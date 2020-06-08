# TabularCsv

This plugin can parse most tabular CSV data files into AQTS field visit data.

This plugin works on AQTS 2019.4-or-newer systems.

## What does 'tabular' mean?

In this context, 'tabular' means data shaped like a database table. A sequence of rows with a common number of columns per row. This is also referred to as a 'flat file'.

Most CSV files follow this general format:

- An optional header section, with a few lines of summary text.
- An optional header row, with one named heading per column.
- Zero or more data rows.

The Tabular plugin can parse these types of files, with some configuration guidance provided by you.

## Check the Wiki for CSV format details and example configuration files

Please see the [plugin wiki](../../../../wiki) for a detailed description of the configuration format and examples.

If you had the following CSV file representing air temperature field visit readings from various locations in your network:

```csv
The Location, The Time, The Temperature
LOC1, 2020-Jun-12 12:35, 20.5
LOC2, 1988-Feb-8 15:10, -3.5
```

Then this configuration description would parse those rows into air temperation field visit readings:
```toml
Name = 'Air temp data file'
FirstLineIsHeader = true

[LocationColumn]
ColumnHeader = 'TheLocation'

[[TimestampColumns]]
Format = 'yyyy-MMM-d h:m'
Type = 'DateTimeOnly'
ColumnHeader = 'TheTime'

[[ReadingColumns]]
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

## Do I need to restart the AQTS server when I make a configuration change?

Nope!

The parser configurations are reloaded, either from disk (in 2020.1-or-older) or from global settings (in 2020.2-or-newer), each time a CSV file is uploaded to Springboard for field visit parsing.

This design choice will aloow you to quickly make changes and try again, without disrupting your entire organization's workflow.