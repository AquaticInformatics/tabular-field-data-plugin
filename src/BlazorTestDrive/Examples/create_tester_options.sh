#!/bin/bash
# Force a stop on first error
set -euxo pipefail

# This will be executed from the $(OutDir) context of the Post-Build Event, the TabularCsv\bin\Release folder
TesterOptions=TesterOptions.txt
RelativePath="..\\..\\..\\BlazorTestDrive\\Examples"

echo "-Data=$RelativePath\\*.csv" > $TesterOptions
for config in *.toml; do
  echo "/Setting=${config%.*}=@$RelativePath\\$config" >> $TesterOptions
done
