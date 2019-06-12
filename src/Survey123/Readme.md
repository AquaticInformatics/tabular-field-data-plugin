# Survey123

This plugin will parse survey results exported from [Survey123](https://survey123.arcgis.com/) in CSV format.

Values from the CSV file will be imported as field visit readings.

## CSV Format

Each survey you configure in Survey123 will yield a different CSV format, with different headers and row content.

So you will need to configure a JSON document which describes which columns in the CSV file map to which activities in AQTS.

### The plugin directory can contain many Survey JSON configurations

When you install the Survey123 plugin, a plugin's directory is scanned for JSON configurations of surveys.

All the `*.json` files in that directory will be loaded, and each CSV file given to the plugin will be matched against one of the survey definitions.

### JSON editing tips

Editing [JSON](https://json.org) can be a tricky thing.

Sometimes the plugin code can detect a poorly formatted JSON document and report a decent error, but sometimes a poorly formatted JSON document will appear to the plugin as just an empty document.

Here are some tips to help eliminate common JSON config errors:
- Edit JSON in a real text editor. Notepad is fine, [Notepad++](https://notepad-plus-plus.org/) or [Visual Studio Code](https://code.visualstudio.com/) are even better choices.
- Don't try editing JSON in Microsoft Word. Word will mess up your quotes and you'll just have a bad time.
- Try validating your JSON using the online [JSONLint validator](https://jsonlint.com/).
- Whitespace between items is ignored. Your JSON document can be single (but very long!) line, but the convention is separate items on different lines, to make the text file more readable.
- All property names must be enclosed in double-quotes (`"`). Don't use single quotes (`'`) or smart quotes (`“` or `”`), which are not that smart for JSON.
- Avoid a trailing comma in lists. JSON is very fussy about using commas **between** list items, but rejects lists when a trailing comma is included. Only use a comma to separate items in the middle of a list.

#### Adding comments to JSON

The JSON spec doesn't support comments, which is unfortunate.

However, the code will simply skip over properties it doesn't care about, so a common trick is to add a dummy property name/value string. The code won't care or complain, and you get to keep some notes close to other special values in your custom JSON document.

Instead of this:

```json
{
  "ExpectedPropertyName": "a value",
  "AnotherExpectedProperty": 12.5 
}
```

Try this:

```json
{
  "_comment_": "Don't enter a value below 12, otherwise things break",
  "ExpectedPropertyName": "a value",
  "AnotherExpectedProperty": 12.5 
}
```

Now your JSON has a comment to help you remember why you chose the `12.5` value.

## Configuration

See [Survey.json](./Survey.json) for an example configuration which parses the [sample](../../data/survey_123_sample.csv) file.

A survey is defined as these properties:
- A `Name` value, which is used when displaying some error messages.
- A `FirstLineIsHeader` boolean value, which tells the parser if the first row is a set of column names.
- A single `LocationColumn`, which defines the CSV column containing AQTS location identifieres
- A set of `TimestampColumns`, which define 1 or more columns from which timestamps will be extracted.
- A set of `CommentColumns` and `PartyColumns`, which can define 0 or more columns containing comment and party values for the visit.
- A set of `ReadingColumns`, which define 1 or more columns from which reading values are extracted. Reading types default to `Routine`, but the survey definition's `ReadingColumn.ReadingType` can be set to any of the allowed values.
- A [`LocationAliases`](#configuring-locationaliases) map, which allows you to remap location identifiers in the CSV to different AQTS locations.

### Column configurations

Each column can be configured with one of two properties:
- A `ColumnHeader` value, which defines the exact header value which defines CSV column
- A `ColumnIndex` value, which defines the column number containing the text. Column indexes start at 1.

Using a `ColumnHeader` is preferred, since it allows the CSV file to change its "shape", adding or removing unrelated columns, without breaking the parsing logic.

A `ColumnHeader` value can only be used when the CSV file contains a header row (ie. if `FirstLineIsHeader` is `true`).

### Configuring `LocationAliases`

The `LocationAliases` property provides a way to accept CSV data from forms with incorrect location information.

Adding a mapping in the `LocationAliases` property can avoid the burden of having to republish a new survey to your Survey123 users.

In this JSON snippet, two locations are remapped.

```json
"LocationAliases": {
  "Old name": "NewAQTSName",
  "Station5": "Station18"
}
```

Notes:
- Editing JSON files [can be tricky](#json-editing-tips). Don't include a trailing comma after the last item in the list.
- Alias lookups are case-insensitive. The above definition will remap "station5" and "STATION5" to "Station18".
- If a location identifier is not found in the `LocationAliases`, then it is assumed to exist in AQTS.
- If a location identifier is not found in AQTS, then its associated readings will not be imported.

If you don't need to remap any locations, then you have two different ways to represent that:

Just delete everything between the braces. Either of these are fine, because whitespace is ignored by JSON.

```json
"LocationAliases": {}
```

or:

```json
"LocationAliases": {
}
```

Or you can completely remove the `LocationAliases` object from the JSON document.

### Timestamp columns

Timestamps are composed of 3 components:
- A date
- A time of day
- An offset from UTC, to make the timestamp unambiguous

Each survey needs to define at least one timestamp column, with a `Format` value describing how to extract date or time components from the column and a `Type` value describing which component(s) to parse from the text column.

For each CSV row processed, the following sequence is applied to determine the actual timestamp:
- Start with a DateTimeOffset value of January 1, 1900, midnight, in the location's local time.
- For each configured timestamp column:
    - Extract the text value of the CSV column
    - Parse the value according to the column's `Format` value via the [DateTimeOffset.TryParseExact()](https://docs.microsoft.com/en-us/dotnet/api/system.datetimeoffset.tryparseexact?view=netframework-4.8#System_DateTimeOffset_TryParseExact_System_String_System_String_System_IFormatProvider_System_Globalization_DateTimeStyles_System_DateTimeOffset__) method
    - Merge the resulting DateTimeOffset value according to the column's `Type` value

| `Type` value | Merge behaviour | Example `Format` |
| --- | --- | --- |
| DateOnly | The newly parsed date replaces the current date.<br/><br/>The current time of day and UTC offset are retained.| `yyyy-M-d` matches all 1-or-2 digit months and day patterns. |
| TimeOnly | The newly parsed time replaces the current time.<br/><br/>The current date and UTC offset are retained.| `H:m` matches all 1-or-2 digit hour & minute patterns on a 24-hour clock. |
| DateTimeOnly | The newly parsed date and time replaces the current date and time.<br/><br/>The current UTC offset is retained.| `yyyy-M-d H:m:s` matches a date time. |
| DateTimeOffset | The newly parsed date, time, and offset are used.<br/><br/>None of the current timestamp components are retained.| [`O`](https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings#the-round-trip-o-o-format-specifier) matches any ISO 8601 timestamp. |
| DateAndSurvey123Offset | The newly parsed date replaces the current date.<br/>The newly parsed **time of day** replaces the current UTC offset.<br/><br/>The current time of day is retained.| `M/d/yyyy h:m:s tt` will parse US-style dates and a time of day of '7:00:00 AM' as a UTC offset of -7 hours.<br/><br/>ESRI, if you are listening, this is the most bizarre UTC offset representation I have seen in quite a while. |

#### Tips about `Format` strings:
`Format` values are [.NET custom date/time format strings](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings).

These format strings can be rather fussy to deal with, so take care to consider some of the common edge cases:
- Format strings are case-sensitive. Common mistakes are made for month-vs-minute and 24-hour-vs-12-hour patterns.
- Uppercase ['M'](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings#M_Specifier) matches month digits, between 1 and 12.
- Lowercase ['m'](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings#mSpecifier) matches minute digits, between 0 and 59.
- Uppercase ['H'](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings#H_Specifier) matches 24-hour hour digits, between 0 and 23.
- Lowercase ['h'](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings#hSpecifier) matches 12-hour hour digits, between 1 and 12, and require a ['t' or 'tt'](https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings#tSpecifier) pattern to distinguish AM from PM.
- Prefer single-character patterns when possible, since they match double-digit values as well. Eg. 'H:m' will match '2:35' and '14:35', but 'HH:mm" will not match '2:35' since the 'HH' means exactly-2-digits.
