# Tabular CSV Field Data Plugin

[![Build status](https://ci.appveyor.com/api/projects/status/rkpwslh6kmrt9pyr/branch/master?svg=true)](https://ci.appveyor.com/project/SystemsAdministrator/tabular-field-data-plugin/branch/master)

An AQTS field data plugin for AQTS 2019.4-or-newer systems, which can read many CSV files of different shapes.

## Want to install this plugin?

- Install the plugin using the System Config page on your AQTS app server.

### Plugin Compatibility Matrix

Choose the appropriate version of the plugin for your AQTS app server.

| AQTS Version | Latest compatible plugin Version |
| --- | --- |
| AQTS 2021.4 Update 1 | [v21.4.6](https://github.com/AquaticInformatics/tabular-field-data-plugin/releases/download/v21.4.6/TabularCsv.plugin) |
| AQTS 2021.4 | [v21.4.0](https://github.com/AquaticInformatics/tabular-field-data-plugin/releases/download/v21.4.0/TabularCsv.plugin) |
| AQTS 2021.3 Update 1 | [v21.3.0](https://github.com/AquaticInformatics/tabular-field-data-plugin/releases/download/v21.3.0/TabularCsv.plugin) |
| AQTS 2021.3<br/>AQTS 2021.2<br/>AQTS 2021.1<br/>AQTS 2020.4<br/>AQTS 2020.3 | [v20.3.16](https://github.com/AquaticInformatics/tabular-field-data-plugin/releases/download/v20.3.16/TabularCsv.plugin) |
| AQTS 2020.2 | [v20.2.13](https://github.com/AquaticInformatics/tabular-field-data-plugin/releases/download/v20.2.13/TabularCsv.plugin) |
| AQTS 2020.1<br/>AQTS 2019.4 Update 1| [v19.4.100](https://github.com/AquaticInformatics/tabular-field-data-plugin/releases/download/v19.4.100/TabularCsv.plugin) |

## CSV file format

See the [plugin wiki](../../wiki) for CSV format and configuration information.

[Test drive the plugin here](https://aquaticinformatics.github.io/tabular-field-data-plugin/test-drive/), from the comfort of your browser!

## What does 'tabular' mean?

In this context, 'tabular' means data shaped like a database table. A sequence of rows with a common number of columns per row. This is also referred to as a 'flat file'.

Most CSV files follow this general format:

- An optional preface section, with a few lines of free-form text.
- An optional header row, with one named heading per column.
- Zero or more data rows.

The Tabular plugin can parse these types of files, with some configuration guidance provided by you.

## Check the Wiki for CSV format details and example configuration files

Please see the [plugin wiki](../../wiki) for a detailed description of the configuration format and examples.

The [class diagram](docs/Readme.md) gives an idea of the property names which can be set.

If you had the following CSV file representing air temperature field visit readings from various locations in your network:

```csv
The Location, The Time, The Temperature
LOC1, 2020-Jun-12 12:35, 20.5
LOC2, 1988-Feb-8 15:10, -3.5
```

Then this configuration description would parse those rows into air temperature field visit readings:
```toml
Location = '@The Location'
Time = '@The Time'

[Reading]
Value = '@The Temperature'
ParameterId = 'TA'
UnitId = 'degC'
```

The format of the configuration description looks a bit like older Windows INI files, but is actually the [TOML format](https://toml.io/en/), but you don't really need to understand the inner workings of TOML. Instead you'll just need to read through the wiki examples and find something similar to your data and modify it from there.

The TOML syntax is quite forgiving and yet still concise. It is much easier for non-programmers to edit vs. more widely used formats like JSON or XML, which can both be quite finicky for humans.

## Where is each configuration file stored?

The plugin can support many different configurations at once, with each configuration stored in a separate file.

The plugin will quickly try each configuration until it finds one that matches your data, and then it will parse the CSV file according to the configuration tules.

| System | TOML storage location | Configuration filenames |
|---|---|---|
| AQTS 2020.1<br/>AQTS 2019.4 | `%ProgramData%\Aquatic Informatics\AQUARIUS Server\Configuration\TabularCSV` <br/><br/> You may need to create this folder on the AQTS app server. | Configuration files can have any name, but must use the `.toml` file extension. |
| Field Visit Hot Folder Service | `%ProgramData%\Aquatic Informatics\AQUARIUS Server\Configuration\TabularCSV` <br/><br/> You may need to create this folder on the computer running the Field Visit Hot Folder Service. | Configuration files can have any name, but must use the `.toml` file extension. |
| AQTS 2020.2-or-newer | Stored in the DB as Global Settings, editable from the System Config page. <br/><br/> Hooray! You don't need direct access to the AQTS app server file system! | **Group**: `FieldDataPluginConfig-TabularCsv`<br/>**Key**: _A name you choose_ (alphanumeric, underscore, or dashes. No whitespace or periods)<br/>**Value**: _Paste your multi-line TOML configuration text here_ |

### AQTS 2019.4 and 2020.1

An AQTS server configuration directory is needed to store all the active configuration files.

The `%ProgramData%\Aquatic Informatics\AQUARIUS Server\Configuration\TabularCSV` folder is where you should place your configuration files.

There will already be an existing `%ProgramData%\Aquatic Informatics\AQUARIUS Server` folder on an AQTS app server, so you will just need to create:
- the `Configuration` subfolder
- the `Configuration\TabularCSV` subfolder

This folder will remain untouched when you upgrade or uninstall the Tabular plugin, so you won't lost your special configuration.

### AQTS 2020.2-and-newer

For AQTS 2020.2-and-newer, the configuration files can be stored as global settings, which can be configured using the System Config application.

This will be much more convienient for quick changes, since you will not require direct access to the app server's file system.

### FieldVisitHotFolderService

The Tabular plugin can be consumed by the [Field Visit Hot Folder Service](https://github.com/AquaticInformatics/aquarius-field-data-framework/tree/master/src/FieldVisitHotFolderService#field-visit-hot-folder-service), which executes plugins outside of AQTS, and only uploads new visit data parsed from the files.

When executed from the FieldVisitHotFolderService, the Tabular plugin will look for configuration files in the `%ProgramData%\Aquatic Informatics\AQUARIUS Server\Configuration\TabularCSV` folder of the computer running the the FieldVisitHotFolderService.

## Do I need to restart the AQTS server when I make a configuration change?

Nope!

Each time a new CSV file is parsed (because it was uploaded to Springboard or it was detected by the FieldVisitHotFolderService), all the configuration files will be reloaded.

This design choice will allow you to quickly make changes, save the updated configuration, and try again, without disrupting your entire organization's workflow.
