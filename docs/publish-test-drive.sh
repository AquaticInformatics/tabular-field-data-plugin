#!/bin/bash

# Manually run this after an explicit Publish operation.

# The default publish layout won't work on GitHub pages. *sigh*

rm -Rf test-drive
mkdir -p test-drive

echo "* binary" > test-drive/.gitattributes

cp -R ../src/BlazorTestDrive/bin/Release/net5.0/browser-wasm/publish/wwwroot/* test-drive
touch test-drive/.nojekyll

# # Rename the _framework directory to lose the leading underscore, which confuses Jekyll even when you tell it not to be confused.
# mv test-drive/_framework test-drive/framework
# 
# pushd test-drive/framework
# # There are two Javascript files which contain the leading underscore as well.
# # Use sed to correct those.
# sed -e "s/_framework/framework/g" -i blazor.webassembly.js
# cd ..
# # Also correct the link in the main page
# sed -e "s/_framework/framework/g" -i index.html
# popd
