#!/bin/bash

# Helper functions
exit_abort () {
	[ ! -z "$1" ] && echo ERROR: "$1"
	echo
	echo 'ABORTED!'
	exit $ERRCODE
}

Tester=packages/Aquarius.FieldDataFramework.20.3.0/tools/PluginTester.exe

Configuration=$1

[ ! -z "$Configuration" ] || Configuration=Release

mkdir results

for example in `basename --multiple --suffix=.toml BlazorTestDrive/Examples/*.toml`; do
 $Tester -Plugin=TabularCsv/bin/${Configuration}/TabularCsv.dll -Json=results -Data=BlazorTestDrive/Examples/${example}.csv -Setting=${example}=@BlazorTestDrive/Examples/${example}.toml || exit_abort
done

echo "All tests successful."
