# TabularCsv

This plugin can parse most tabular CSV data files into AQTS field visit data.

This plugin works on AQTS 2019.4-or-newer systems.

## Requirements for building the plugin from source

- Requires Visual Studio 2017 (Community Edition is fine)
- .NET 4.7.2 runtime

## Building the plugin

- Load the `src\TabularCsv.sln` file in Visual Studio and build the `Release` configuration.
- The `src\TabularCsv\deploy\Release\TabularCsv.plugin` file can then be installed on your AQTS app server.

## Testing the plugin within Visual Studio

Use the included `PluginTester.exe` tool from the `Aquarius.FieldDataFramework` package to test your plugin logic on the sample files.

1. Open the TabularCsv project's **Properties** page
2. Select the **Debug** tab
3. Select **Start external program:** as the start action and browse to `"src\packages\Aquarius.FieldDataFramework.20.3.0\tools\PluginTester.exe`
4. Enter the **Command line arguments:** to launch your plugin

```
/Plugin=TabularCsv.dll /Json=AppendedResults.json /Data=..\..\..\..\data\survey_123_sample.csv /Setting=MyConfig=@path\to\MyConfig.toml
```

The `/Plugin=` argument can be the filename of your plugin assembly, without any folder. The default working directory for a start action is the bin folder containing your plugin.

5. Set a breakpoint in the plugin's `ParseFile()` methods.
6. Select your plugin project in Solution Explorer and select **"Debug | Start new instance"**
7. Now you're debugging your plugin!

See the [PluginTester](https://github.com/AquaticInformatics/aquarius-field-data-framework/tree/master/src/PluginTester) documentation for more details.
