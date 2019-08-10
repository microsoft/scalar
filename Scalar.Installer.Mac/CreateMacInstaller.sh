#!/bin/bash

SOURCEDIRECTORY=$1
if [ -z $SOURCEDIRECTORY ]; then
  echo "Error: Source directory not specified"
  exit 1
fi

CONFIGURATION=$2
if [ -z $CONFIGURATION ]; then
  echo "Error: Build configuration not specified"
  exit 1
fi

PACKAGEVERSION=$3
if [ -z $PACKAGEVERSION ]; then
  echo "Error: Installer package version not specified"
  exit 1
fi

BUILDOUTPUTDIR=${4%/}
if [ -z $BUILDOUTPUTDIR ]; then
  echo "Error: Build output directory not specified"
  exit 1
fi

if [ -z $Scalar_OUTPUTDIR ]; then
  echo "Error: Missing environment variable. Scalar_OUTPUTDIR is not set"
  exit 1
fi

if [ -z $Scalar_PUBLISHDIR ]; then
  echo "Error: Missing environment variable. Scalar_PUBLISHDIR is not set"
  exit 1
fi

STAGINGDIR="${BUILDOUTPUTDIR}/Staging"
PACKAGESTAGINGDIR="${BUILDOUTPUTDIR}/Packages"
ScalarFORGITDESTINATION="usr/local/scalar"
DAEMONPLISTDESTINATION="Library/LaunchDaemons"
AGENTPLISTDESTINATION="Library/LaunchAgents"
LIBRARYEXTENSIONSDESTINATION="Library/Extensions"
LIBRARYAPPSUPPORTDESTINATION="Library/Application Support/Scalar"
INSTALLERPACKAGENAME="Scalar.$PACKAGEVERSION"
INSTALLERPACKAGEID="com.scalar.pkg"
UNINSTALLERPATH="${SOURCEDIRECTORY}/uninstall_scalar.sh"
SCRIPTSPATH="${SOURCEDIRECTORY}/scripts"
COMPONENTSPLISTPATH="${SOURCEDIRECTORY}/scalar_components.plist"
DIST_FILE_NAME="Distribution.updated.xml"

function CheckBuildIsAvailable()
{
    if [ ! -d "$Scalar_OUTPUTDIR" ] || [ ! -d "$Scalar_PUBLISHDIR" ]; then
        echo "Error: Could not find Scalar Build to package."
        exit 1
    fi
}

function SetPermissions()
{
    chmodCommand="chmod -R 755 \"${STAGINGDIR}\""
    eval $chmodCommand || exit 1
}
 
function CreateInstallerRoot()
{
    mkdirVfsForGit="mkdir -p \"${STAGINGDIR}/$ScalarFORGITDESTINATION\""
    eval $mkdirVfsForGit || exit 1
    
    mkdirPkgStaging="mkdir -p \"${PACKAGESTAGINGDIR}\""
    eval $mkdirPkgStaging || exit 1

    mkdirBin="mkdir -p \"${STAGINGDIR}/usr/local/bin\""
    eval $mkdirBin || exit 1
    
    mkdirBin="mkdir -p \"${STAGINGDIR}/$LIBRARYEXTENSIONSDESTINATION\""
    eval $mkdirBin || exit 1
    
    mkdirBin="mkdir -p \"${STAGINGDIR}/$LIBRARYAPPSUPPORTDESTINATION\""
    eval $mkdirBin || exit 1
    
    mkdirBin="mkdir -p \"${STAGINGDIR}/$DAEMONPLISTDESTINATION\""
    eval $mkdirBin || exit 1
    
    mkdirBin="mkdir -p \"${STAGINGDIR}/$AGENTPLISTDESTINATION\""
    eval $mkdirBin || exit 1
}

function CopyBinariesToInstall()
{
    copyPublishDirectory="cp -Rf \"${Scalar_PUBLISHDIR}\"/* \"${STAGINGDIR}/${ScalarFORGITDESTINATION}/.\""
    eval $copyPublishDirectory || exit 1
    
    removeTestAssemblies="find \"${STAGINGDIR}/${ScalarFORGITDESTINATION}\" -name \"*Scalar.*Tests*\" -exec rm -f \"{}\" \";\""
    eval $removeTestAssemblies || exit 1
    
    removeDataDirectory="rm -Rf \"${STAGINGDIR}/${ScalarFORGITDESTINATION}/Data\""
    eval $removeDataDirectory || exit 1
        
    copyUnInstaller="cp -f \"${UNINSTALLERPATH}\" \"${STAGINGDIR}/${ScalarFORGITDESTINATION}/.\""
    eval $copyUnInstaller || exit 1
    
    copyNotificationApp="cp -Rf \"${Scalar_OUTPUTDIR}/Scalar.Notifications/Scalar.Mac/Build/Products/$CONFIGURATION/Scalar.app\" \"${STAGINGDIR}/${LIBRARYAPPSUPPORTDESTINATION}/.\""
    eval $copyNotificationApp || exit 1
    
    copyNotificationPlist="cp -Rf \"${SOURCEDIRECTORY}/../Scalar.Notifications/Scalar.Mac/org.scalar.usernotification.plist\" \"${STAGINGDIR}/${AGENTPLISTDESTINATION}/.\""
    eval $copyNotificationPlist || exit 1
    
    copyServicePlist="cp -Rf \"${SOURCEDIRECTORY}/../Scalar.Service/Mac/org.scalar.service.plist\" \"${STAGINGDIR}/${AGENTPLISTDESTINATION}/.\""
    eval $copyServicePlist || exit 1
    
    currentDirectory=`pwd`
    cd "${STAGINGDIR}/usr/local/bin"
    linkCommand="ln -sf ../scalar/scalar scalar"
    eval $linkCommand
    cd $currentDirectory
}

function CreateScalarInstaller()
{
    pkgBuildCommand="/usr/bin/pkgbuild --identifier $INSTALLERPACKAGEID --component-plist \"${COMPONENTSPLISTPATH}\" --scripts \"${SCRIPTSPATH}\" --root \"${STAGINGDIR}\" \"${PACKAGESTAGINGDIR}/$INSTALLERPACKAGENAME.pkg\""
    eval $pkgBuildCommand || exit 1
}

function UpdateDistributionFile()
{
    ScalarFORGIT_PKG_VERSION=$PACKAGEVERSION
    ScalarFORGIT_PKG_NAME="$INSTALLERPACKAGENAME.pkg"
    GIT_PKG_NAME=$1
    GIT_PKG_VERSION=$2
        
    /usr/bin/sed -e "s|ScalarFORGIT_VERSION_PLACHOLDER|$ScalarFORGIT_PKG_VERSION|g" "$SCRIPTSPATH/Distribution.xml" > "${BUILDOUTPUTDIR}/$DIST_FILE_NAME"
    /usr/bin/sed -i.bak "s|ScalarFORGIT_PKG_NAME_PLACEHOLDER|$ScalarFORGIT_PKG_NAME|g" "${BUILDOUTPUTDIR}/$DIST_FILE_NAME"
    
    if [ ! -z "$GIT_PKG_NAME" ] && [ ! -z "$GIT_PKG_VERSION" ]; then
        GIT_CHOICE_OUTLINE_ELEMENT_TEXT="<line choice=\"com.git.pkg\"/>"
        GIT_CHOICE_ID_ELEMENT_TEXT="<choice id=\"com.git.pkg\" visible=\"false\"> <pkg-ref id=\"com.git.pkg\"/> </choice>"
        GIT_PKG_REF_ELEMENT_TEXT="<pkg-ref id=\"com.git.pkg\" version=\"$GIT_PKG_VERSION\" onConclusion=\"none\">$GIT_PKG_NAME</pkg-ref>"
    else
        GIT_CHOICE_OUTLINE_ELEMENT_TEXT=""
        GIT_CHOICE_ID_ELEMENT_TEXT=""
        GIT_PKG_REF_ELEMENT_TEXT=""
    fi
    
    /usr/bin/sed -i.bak "s|GIT_CHOICE_OUTLINE_PLACEHOLDER|$GIT_CHOICE_OUTLINE_ELEMENT_TEXT|g" "${BUILDOUTPUTDIR}/$DIST_FILE_NAME"
    /usr/bin/sed -i.bak "s|GIT_CHOICE_ID_PLACEHOLDER|$GIT_CHOICE_ID_ELEMENT_TEXT|g" "${BUILDOUTPUTDIR}/$DIST_FILE_NAME"
    /usr/bin/sed -i.bak "s|GIT_PKG_REF_PLACEHOLDER|$GIT_PKG_REF_ELEMENT_TEXT|g" "${BUILDOUTPUTDIR}/$DIST_FILE_NAME"
    
    /bin/rm -f "${BUILDOUTPUTDIR}/$DIST_FILE_NAME.bak"
}

function CreateScalarDistribution()
{
    # Update distribution file(removes Git info from template.)
    UpdateDistributionFile "" ""
    
    buildScalarDistCmd="/usr/bin/productbuild --distribution \"${BUILDOUTPUTDIR}/Distribution.updated.xml\" --package-path \"$PACKAGESTAGINGDIR\" \"${BUILDOUTPUTDIR}/$INSTALLERPACKAGENAME.pkg\""
    echo $buildScalarDistCmd
    eval $buildScalarDistCmd || exit 1
    
    /bin/rm -f "${BUILDOUTPUTDIR}/$DIST_FILE_NAME"
}

function CreateMetaDistribution()
{
    GITVERSION="$($Scalar_SCRIPTDIR/GetGitVersionNumber.sh)"
    GITINSTALLERPKGPATH="$(find $Scalar_PACKAGESDIR/gitformac.scalar.installer/$GITVERSION -type f -name *.pkg)" || exit 1

    GITPKGNAME="${GITINSTALLERPKGPATH##*/}"
    GITINSTALLERPKGNAME="${GITPKGNAME%.pkg}"
    GITVERSIONSTRING=`echo $GITINSTALLERPKGNAME | cut -d"-" -f2`

    if [[ -z "$GITVERSION" || -z "$GITVERSIONSTRING" ]]; then
        echo "Error creating metapackage: could not determine Git package version."
        exit 1
    fi
    
    if [ ! -f "$GITINSTALLERPKGPATH" ]; then
        echo "Error creating metapackage: could not find Git installer package."
        exit 1
    fi
    
    copyGitInstallerPkgToStgCmd="/bin/cp -Rf \"${GITINSTALLERPKGPATH}\" \"${PACKAGESTAGINGDIR}/.\""
    echo $copyGitInstallerPkgToStgCmd
    eval $copyGitInstallerPkgToStgCmd || exit 1

    UpdateDistributionFile "$GITPKGNAME" "$GITVERSIONSTRING"

    METAPACKAGENAME="$INSTALLERPACKAGENAME-Git.$GITVERSION.pkg"
    buildMetapkgCmd="/usr/bin/productbuild --distribution \"${BUILDOUTPUTDIR}/Distribution.updated.xml\" --package-path \"$PACKAGESTAGINGDIR\" \"${BUILDOUTPUTDIR}/$METAPACKAGENAME\""
    echo $buildMetapkgCmd
    eval $buildMetapkgCmd || exit 1
    
    /bin/rm -f "${BUILDOUTPUTDIR}/$DIST_FILE_NAME"
}

function Run()
{
    CheckBuildIsAvailable
    CreateInstallerRoot
    CopyBinariesToInstall
    SetPermissions
    CreateScalarInstaller
    CreateScalarDistribution
    CreateMetaDistribution
}

Run
