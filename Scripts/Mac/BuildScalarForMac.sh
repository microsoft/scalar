#!/bin/bash

. "$(dirname ${BASH_SOURCE[0]})/InitializeEnvironment.sh"

CONFIGURATION=$1
if [ -z $CONFIGURATION ]; then
  CONFIGURATION=Debug
fi

runVersionUpdater="yes"
VERSION=$2
if [ -z $VERSION ]; then
  VERSION="0.2.173.2"
  
  # don't update version number for developer builds
  runVersionUpdater="no"
fi

if [ ! -d $Scalar_OUTPUTDIR ]; then
  mkdir $Scalar_OUTPUTDIR
fi

# Create the directory where we'll do pre build tasks
BUILDDIR=$Scalar_OUTPUTDIR/Scalar.Build
if [ ! -d $BUILDDIR ]; then
  mkdir $BUILDDIR || exit 1
fi

echo 'Downloading a Scalar-enabled version of Git...'
$Scalar_SCRIPTDIR/DownloadScalarGit.sh || exit 1
GITVERSION="$($Scalar_SCRIPTDIR/GetGitVersionNumber.sh)"
GITPATH="$(find $Scalar_PACKAGESDIR/gitformac.gvfs.installer/$GITVERSION -type f -name *.dmg)" || exit 1
echo "Downloaded Git $GITVERSION"
# Now that we have a path containing the version number, generate ScalarConstants.GitVersion.cs
$Scalar_SCRIPTDIR/GenerateGitVersionConstants.sh "$GITPATH" $BUILDDIR || exit 1

# If we're building the Profiling(Release) configuration, remove Profiling() for building .NET code
if [ "$CONFIGURATION" == "Profiling(Release)" ]; then
  CONFIGURATION=Release
fi

echo "Generating CommonAssemblyVersion.cs as $VERSION..."
$Scalar_SCRIPTDIR/GenerateCommonAssemblyVersion.sh $VERSION || exit 1

# /warnasmessage:MSB4011. Reference: https://bugzilla.xamarin.com/show_bug.cgi?id=58564
# Visual Studio Mac does not support explicit import of Sdks. Scalar.Installer.Mac.csproj
# does need this ability to override "Build" and "Publish" targets. As a workaround the 
# project implicitly imports "Microsoft.Net.Sdk" in the beginning of its csproj (because 
# otherwise Visual Studio Mac IDE will not be able to open the Scalar.Install.Mac project) 
# and explicitly imports Project="Sdk.targets" Sdk="Microsoft.NET.Sdk" later, before 
# overriding build targets. The duplicate import generates warning MSB4011 that is ignored
# by this switch.
echo 'Restoring packages...'
dotnet restore $Scalar_SRCDIR/Scalar.sln /p:Configuration=$CONFIGURATION.Mac --packages $Scalar_PACKAGESDIR /warnasmessage:MSB4011 || exit 1
dotnet build $Scalar_SRCDIR/Scalar.sln --runtime osx-x64 --framework netcoreapp2.1 --configuration $CONFIGURATION.Mac /maxcpucount:1 /warnasmessage:MSB4011 || exit 1

NATIVEDIR=$Scalar_SRCDIR/Scalar.Native.Mac
xcodebuild -configuration $CONFIGURATION -workspace $NATIVEDIR/Scalar.Native.Mac.xcworkspace build -scheme Scalar.Native.Mac -derivedDataPath $Scalar_OUTPUTDIR/Scalar.Native.Mac || exit 1

USERNOTIFICATIONDIR=$Scalar_SRCDIR/Scalar.Notifications/Scalar.Mac
USERNOTIFICATIONPROJECT="$USERNOTIFICATIONDIR/Scalar.xcodeproj"
USERNOTIFICATIONSCHEME="Scalar"
if [ "$runVersionUpdater" == "yes" ]; then
    updateAppVersionCmd="(cd \"$USERNOTIFICATIONDIR\" && /usr/bin/xcrun agvtool new-marketing-version \"$VERSION\")"
    echo $updateAppVersionCmd
    eval $updateAppVersionCmd || exit 1
fi
# Build user notification app
xcodebuild -configuration $CONFIGURATION -project "$USERNOTIFICATIONPROJECT" build -scheme "$USERNOTIFICATIONSCHEME" -derivedDataPath $Scalar_OUTPUTDIR/Scalar.Notifications/Scalar.Mac || exit 1

# Build the tests in a separate directory, so the binary for distribution does not contain
# test plugins created and injected by the test build.
xcodebuild -configuration $CONFIGURATION -project "$USERNOTIFICATIONPROJECT" test -scheme "$USERNOTIFICATIONSCHEME" -derivedDataPath $Scalar_OUTPUTDIR/Scalar.Notifications/Scalar.Mac/Tests || exit 1

if [ ! -d $Scalar_PUBLISHDIR ]; then
  mkdir $Scalar_PUBLISHDIR || exit 1
fi

echo 'Copying native binaries to Publish directory...'
cp $Scalar_OUTPUTDIR/Scalar.Native.Mac/Build/Products/$CONFIGURATION/Scalar.ReadObjectHook $Scalar_PUBLISHDIR || exit 1

# Publish after native build, so installer package can include the native binaries.
dotnet publish $Scalar_SRCDIR/Scalar.sln /p:Configuration=$CONFIGURATION.Mac /p:Platform=x64 --runtime osx-x64 --framework netcoreapp2.1 --self-contained --output $Scalar_PUBLISHDIR /maxcpucount:1 /warnasmessage:MSB4011 || exit 1

echo 'Copying Git installer to the output directory...'
$Scalar_SCRIPTDIR/PublishGit.sh $GITPATH || exit 1

echo 'Installing shared data queue stall workaround...'
# We'll generate a temporary project if and only if we don't find the correct dylib already in place.
BUILDDIR=$Scalar_OUTPUTDIR/Scalar.Build
if [ ! -e $BUILDDIR/libSharedDataQueue.dylib ]; then
  cp $Scalar_SRCDIR/nuget.config $BUILDDIR
  dotnet new classlib -n Restore.SharedDataQueueStallWorkaround -o $BUILDDIR --force
  dotnet add $BUILDDIR/Restore.SharedDataQueueStallWorkaround.csproj package --package-directory $Scalar_PACKAGESDIR SharedDataQueueStallWorkaround --version '1.0.0'
  cp $Scalar_PACKAGESDIR/shareddataqueuestallworkaround/1.0.0/libSharedDataQueue.dylib $BUILDDIR/libSharedDataQueue.dylib
fi

echo 'Running Scalar unit tests...'
$Scalar_PUBLISHDIR/Scalar.UnitTests || exit 1
