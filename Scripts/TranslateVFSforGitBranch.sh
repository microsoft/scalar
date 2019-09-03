#!/bin/bash

# Attempt to translate code from VFS for Git to Scalar.
# Usage: ./TranslateVFSforGitBranch.sh BASE TOPIC

BASE=$1
TOPIC=$2

git remote add vfs https://github.com/microsoft/vfsforgit
git fetch vfs

rm -rf .vfs-translations
echo "git format-patch -o .vfs-translations/ $BASE..$TOPIC"
git format-patch -o .vfs-translations/ "$BASE".."$TOPIC"

for patch in $(ls .vfs-translations)
do
	file=".vfs-translations/$patch"

	# Drop starting GVFS folder
	sed -i .bak "s/a\\/GVFS\\//a\\//g" "$file"
	sed -i .bak "s/b\\/GVFS\\//b\\//g" "$file"

	# Replace GVFS and friends with Scalar
        sed -i .bak "s/GVFS/Scalar/g" "$file"
        sed -i .bak "s/VFS For Git/Scalar/g" "$file"
        sed -i .bak "s/VFS for Git/Scalar/g" "$file"
        sed -i .bak "s/VFSForGit/Scalar/g" "$file"
        sed -i .bak "s/vfsforgit/scalar/g" "$file"
        sed -i .bak "s/VFS/Scalar/g" "$file"
        sed -i .bak "s/gvfs/scalar/g" "$file"
done

git fetch origin
git checkout origin/master

for patch in $(ls .vfs-translations)
do
	file=".vfs-translations/$patch"
	git am <"$file"
done
