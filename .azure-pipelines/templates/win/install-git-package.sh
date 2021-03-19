#!/bin/bash

GIT_VERSION=$(grep '<GitPackageVersion>' Directory.Build.props | grep -Eo '[0-9.]+(-\w+)*')
mkdir ../out
cd ../out
dotnet new classlib -n Scalar.GitInstaller
cd Scalar.GitInstaller
dotnet add Scalar.GitInstaller.csproj package "GitForWindows.GVFS.Installer" --package-directory . --version "$GIT_VERSION" --source "https://pkgs.dev.azure.com/gvfs/ci/_packaging/Dependencies/nuget/v3/index.json"
