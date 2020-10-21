#!/bin/bash
die () {
    echo "$*" >&2
    exit 1
}

make_absolute () {
    case "$1" in
    /*)
        echo "$1"
        ;;
    *)
        echo "$PWD/$1"
        ;;
    esac
}

#####################################################################
# Building
#####################################################################
echo "Building Scalar.Packaging.Linux..."

# Parse script arguments
VERSION=$1
if [ -z "${VERSION}" ]; then
  die "Error: Version not specified"
fi

CONFIGURATION=$2
CONFIGURATION="${CONFIGURATION:=Debug}"

# Directories
THISDIR="$( cd "$(dirname "$0")" ; pwd -P )"
ROOT="$( cd "$THISDIR"/../.. ; pwd -P )"
SRC="$( cd "$THISDIR"/.. ; pwd -P )"
OUT="$ROOT/out"
SCALAR_SRC="$SRC/Scalar"
PROJ_OUT="$OUT/Scalar.Packaging.Linux"

# Build parameters
FRAMEWORK=netcoreapp3.1
RUNTIME=linux-x64

ARCH="`dpkg-architecture -q DEB_HOST_ARCH`"
if test -z "$ARCH"; then
  die "Could not determine host architecture!"
fi

# Outputs
PAYLOAD="$PROJ_OUT/payload/$CONFIGURATION"
SYMBOLOUT="$PROJ_OUT/payload.sym/$CONFIGURATION"

TAROUT="$PROJ_OUT/tar/$CONFIGURATION"
TARBALL="$TAROUT/scalar-linux_$ARCH.$VERSION.tar.gz"
SYMTARBALL="$TAROUT/symbols-linux_$ARCH.$VERSION.tar.gz"

DEBOUT="$PROJ_OUT/deb/$CONFIGURATION"
DEBROOT="$DEBOUT/root"
DEBPKG="$DEBOUT/scalar-linux_$ARCH.$VERSION.deb"
DEBROOTAZREPOS="$DEBOUT/root-azrepos"
DEBPKGAZREPOS="$DEBOUT/scalar-azrepos-linux_$ARCH.$VERSION.deb"

# Cleanup payload directory
if [ -d "$PAYLOAD" ]; then
    echo "Cleaning existing payload directory '$PAYLOAD'..."
    rm -rf "$PAYLOAD"
fi

# Cleanup symbol directory
if [ -d "$SYMBOLOUT" ]; then
    echo "Cleaning existing symbols directory '$SYMBOLOUT'..."
    rm -rf "$SYMBOLOUT"
fi

# Ensure directories exists
mkdir -p "$PAYLOAD" "$SYMBOLOUT" "$DEBROOT" "$DEBROOTAZREPOS"

# Publish core application executables
echo "Publishing core application..."
dotnet publish "$SCALAR_SRC" \
    --configuration="$CONFIGURATION" \
    --framework="$FRAMEWORK" \
    --runtime="$RUNTIME" \
    --self-contained=true \
    -p:ScalarVersion=$VERSION \
    -p:PublishSingleFile=True \
    --output="$(make_absolute "$PAYLOAD")" || exit 1

# Collect symbols
echo "Collecting managed symbols..."
mv "$PAYLOAD"/*.pdb "$SYMBOLOUT" || exit 1

echo "Build complete."

#####################################################################
# PACKING
#####################################################################
echo "Packing Scalar.Packaging.Linux..."
# Cleanup any old archive files
if [ -e "$TAROUT" ]; then
    echo "Deleting old archive '$TAROUT'..."
    rm "$TAROUT"
fi

# Ensure the parent directory for the archive exists
mkdir -p "$TAROUT" || exit 1

# Set full read, write, execute permissions for owner and just read and execute permissions for group and other
echo "Setting file permissions..."
/bin/chmod -R 755 "$PAYLOAD" || exit 1

# Build binaries tarball
echo "Building binaries tarball..."
pushd "$PAYLOAD"
tar -czvf "$TARBALL" * || exit 1
popd

# Build symbols tarball
echo "Building symbols tarball..."
pushd "$SYMBOLOUT"
tar -czvf "$SYMTARBALL" * || exit 1
popd

# Build .deb
INSTALL_TO="$DEBROOT/usr/local/bin/"
INSTALL_TOAZREPOS="$DEBROOTAZREPOS/usr/local/bin/"
mkdir -p "$DEBROOT/DEBIAN" "$DEBROOTAZREPOS/DEBIAN" "$INSTALL_TO" "$INSTALL_TOAZREPOS" || exit 1

# Make the debian control files
cat >"$DEBROOT/DEBIAN/control" <<EOF
Package: scalar
Version: $VERSION
Section: vcs
Priority: optional
Architecture: $ARCH
Pre-Depends: git (>= 1:2.25.1)
Recommends: gcmcore
Maintainer: Git Client Team <git-client@github.com>
Description: A set of tools and extensions for Git to allow very large
 monorepos to run on Git without a virtualization layer.
 For more information see https://aka.ms/scalar
EOF

cat >"$DEBROOTAZREPOS/DEBIAN/control" <<EOF
Package: scalar-azrepos
Version: $VERSION
Section: vcs
Priority: optional
Architecture: $ARCH
Pre-Depends: git-vfs (>= 2.28.0.vfs.1.0)
Recommends: gcmcore
Maintainer: Git Client Team <git-client@github.com>
Description: A set of tools and extensions for Git to allow very large
 monorepos to run on Git without a virtualization layer.
 For more information see https://aka.ms/scalar
EOF

# Copy single binary to target installation location
cp "$PAYLOAD/scalar" "$INSTALL_TO" || exit 1
cp "$PAYLOAD/scalar" "$INSTALL_TOAZREPOS" || exit 1

dpkg-deb --build "$DEBROOT" "$DEBPKG" || exit 1
dpkg-deb --build "$DEBROOTAZREPOS" "$DEBPKGAZREPOS" || exit 1

echo "Pack complete."
