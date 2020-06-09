# Tabular CSV Field Data Plugin

[![Build status](https://ci.appveyor.com/api/projects/status/rkpwslh6kmrt9pyr/branch/master?svg=true)](https://ci.appveyor.com/project/SystemsAdministrator/tabular-field-data-plugin/branch/master)

An AQTS field data plugin for AQTS 2019.4-or-newer systems, which can read many CSV files of different shapes.

## Want to install this plugin?

- Download the latest release of the plugin [here](../../releases/latest)
- Install it using the System Config page on your AQTS app server.

## CSV file format

See the [plugin wiki](../../wiki) for CSV format and configuration information.

## What does 'tabular' mean?

In this context, 'tabular' means data shaped like a database table. A sequence of rows with a common number of columns per row. This is also referred to as a 'flat file'.

Most CSV files follow this general format:

- An optional header section, with a few lines of summary text.
- An optional header row, with one named heading per column.
- Zero or more data rows.

The Tabular plugin can parse these types of files, with some configuration guidance provided by you.

## Check the Wiki for CSV format details and example configuration files

Please see the [plugin wiki](../../wiki) for a detailed description of the configuration format and examples.

If you had the following CSV file representing air temperature field visit readings from various locations in your network:

```csv
The Location, The Time, The Temperature
LOC1, 2020-Jun-12 12:35, 20.5
LOC2, 1988-Feb-8 15:10, -3.5
```

Then this configuration description would parse those rows into air temperation field visit readings:
```toml
Name = 'Air temp data file'

[Location]
ColumnHeader = 'The Location'

[[Timestamps]]
Format = 'yyyy-MMM-d h:m'
Type = 'DateTimeOnly'
ColumnHeader = 'The Time'

[[Readings]]
ParameterId = 'TA'
UnitId = 'degC'
ColumnHeader = 'The Temperature'
```

The format of the configuration description looks a bit like older Windows INI files, but is actually the [TOML format](https://github.com/toml-lang/toml/blob/master/README.md#example), but you don't really need to understand the inner workings of TOML. Instead you'll just need to read through the wiki examples and find something similar to your data and modify it from there.

The TOML syntax is quite forgiving and yet still concise. It is much easier for non-programmers to edit vs. more widely used formats like JSON or XML, which can both be quite finicky for humans.

## Where is each configuration file stored?

The plugin can support many different configurations at once, with each configuration stored in a separate file.

The plugin will quickly try each configuration until it finds one that matches your data, and then it will parse the CSV file according to the configuration tules.

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
