#!/bin/bash

# Manually run this after an explicit Publish operation.

# The default publish layout won't work on GitHub pages. *sigh*

rm -Rf test-drive
mkdir -p test-drive

echo "* binary" > test-drive/.gitattributes

dotnet publish ../src/BlazorTestDrive/BlazorTestDrive.csproj -c Release -o release --nologo

cp -R release/wwwroot/* test-drive
touch test-drive/.nojekyll
