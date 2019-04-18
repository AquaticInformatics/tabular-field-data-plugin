# Survey123

This plugin will parse survey results exported from [Survey123](https://survey123.arcgis.com/) in CSV format.

Values from the CSV file will be imported as field visit readings.

## CSV Format

Each survey you configure in Survey123 will yield a different CSV format, with different headers and row content.

So you will need to configure a JSON document which describes which columns in the CSV file map to which activities in AQTS.

## Configuration

See [Survey.json](./Survey.json) for an example configuration which parses the [sample](../../data/survey_123_sample.csv) file.
