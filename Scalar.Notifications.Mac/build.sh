#!/bin/bash

CONFIGURATION=$1
if [ -z $CONFIGURATION ]; then
  echo "Error: Build configuration not specified"
  exit 1
fi

INT_DIR=$2
if [ -z "${INT_DIR}" ]; then
  echo "Error: Intermediate output directory not specified"
  exit 1
fi

OUT_DIR=$3
if [ -z "${OUT_DIR}" ]; then
  echo "Error: Output directory not specified"
  exit 1
fi

VERSION=$4
if [ -z "${VERSION}" ]; then
  echo "Info: Version not set; not updating version number"
fi

THIS_DIR="$( cd "$(dirname "$0")" ; pwd -P )"

# Set the version if not the default 'developer version'
if [ -n "$VERSION" ] && [ "$VERSION" != "0.2.173.2" ]; then
    updateAppVersionCmd="(cd \"${THIS_DIR}\" && /usr/bin/xcrun agvtool new-marketing-version \"$VERSION\")"
    eval $updateAppVersionCmd || exit 1
fi

# Build the product
xcodebuild -configuration "$CONFIGURATION" -project "${THIS_DIR}/Scalar.xcodeproj" build -scheme "Scalar" -derivedDataPath "$INT_DIR" || exit 1

# Ensure the output directory exists
mkdir -p "$OUT_DIR" || exit 1

# Copy output from intermediate output to final binary output directory
cp -Rf "$INT_DIR/Build/Products/$CONFIGURATION/Scalar.app" "$OUT_DIR" || exit 1
