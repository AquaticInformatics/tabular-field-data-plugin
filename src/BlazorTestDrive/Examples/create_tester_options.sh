#!/bin/bash
# Force a stop on first error
set -euxo pipefail

# This will be executed from the $(TargetDir) context of the PackageAndTestPlugin MSBuild target,
# e.g. TabularCsv\bin\Release\net472 or TabularCsv\bin\Release\net10.0
TesterOptions=TesterOptions.txt
RelativePath="..\\..\\..\\..\\BlazorTestDrive\\Examples"

echo "-Data=$RelativePath\\*.csv" > $TesterOptions
for config in *.toml; do
  echo "/Setting=${config%.*}=@$RelativePath\\$config" >> $TesterOptions
done
