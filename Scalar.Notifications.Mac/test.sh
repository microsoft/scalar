#!/bin/bash

CONFIGURATION=$1
if [ -z $CONFIGURATION ]; then
  echo "Error: Build configuration not specified"
  exit 1
fi

INT_DIR=$2
if [ -z $INT_DIR ]; then
  echo "Error: Intermediate output directory not specified"
  exit 1
fi

# Run tests
xcodebuild -configuration "$CONFIGURATION" -project "Scalar.xcodeproj" test -scheme "Scalar" -derivedDataPath "$INT_DIR" || exit 1
