using System.Text;

namespace BlazorTestDrive
{
    public class Examples
    {
        public static List<Example> AllExamples { get; } = new List<Example>
        {
            new Example
            {
                Id = "AirTemperature",
                Name = "Air Temperature readings",
            },
            new Example
            {
                Id = "SeparateTimeAndDateColumns",
                Name = "Air Temperature readings (separate date and time columns)",
                Description = "Same as the previous example, but the date and time values are in separate columns",
            },
            new Example
            {
                Id = "French",
                Name = "Parse French dates and numbers",
                Description = "Uses the .NET French locale to parse dates and numbers.",
            },
            new Example
            {
                Id = "Spanish",
                Name = "Parse Spanish dates and numbers",
                Description = "Uses the .NET Spanish locale to parse dates and numbers.",
            },
            new Example
            {
                Id = "ReadingsWithGrades",
                Name = "Readings with grades and qualifiers",
                Description = "This example shows how to bring in more than one qualifier per reading, using a semi-colon to separate qualifiers in your CSV data column.",
            },
            new Example
            {
                Id = "ReadingsWithDatumConversion",
                Name = "Readings with datum conversion",
                Description = "This example shows how to bring in some Depth readings with optional datum conversion context, along with other reading types.",
            },
            new Example
            {
                Id = "StageDischargeReadingsFormat",
                Name = "The StageDischargeReadings plugin format",
                Description = @"
This configuration emulates the now-obsolete
<a href=""https://github.com/AquaticInformatics/stage-discharge-readings-field-data-plugin#stage-discharge-readings-field-data-plugin"">StageDischargeReadings plugin</a>.
Note that it also includes a sparse list of <a href=""https://github.com/AquaticInformatics/tabular-field-data-plugin/wiki/GageHeightMeasurements""<code>[[PanelDischargeSummary.GageHeightMeasurements]]</code></a> sections.",
            },
            new Example
            {
                Id = "Aliases",
                Name = "Use {Aliases} to transform your data",
                Description = "Aliases are a power feature for transforming invalid data from your CSV file into a form required by AQUARIUS. Note how the Unicode degree symbol in the CSV is transformed to degC or degF.",
            },
            new Example
            {
                Id = "Survey123Example",
                Name = "Read exports from a Survey123 form",
                Description = @"
Parses readings collected from a custom Survey123 survey.
Uses aliases to work around old stations that have been renamed in AQUARIUS.",
            },
            new Example
            {
                Id = "OttMFPro",
                Name = "OTT MF Pro *.TSV files",
                Description = "Uses plenty of regular expressions to pull out data from an OTT MF Pro discharge summary file.",
            }
        }
            .Select(LoadEmbeddedResources)
            .ToList();

        private static Example LoadEmbeddedResources(Example example)
        {
            if (string.IsNullOrWhiteSpace(example.ConfigText))
                example.ConfigText = EmbeddedResourceLoader.LoadAsText(Path.Combine("Examples", $"{example.Id}.toml"));

            if (string.IsNullOrWhiteSpace(example.CsvText))
                example.CsvText = EmbeddedResourceLoader.LoadAsText(Path.Combine("Examples", $"{example.Id}.csv"), GetEncoding(example.EncodingName));

            return example;
        }

        private static Encoding? GetEncoding(string? encodingName)
        {
            encodingName = encodingName?.Trim();

            if (string.IsNullOrEmpty(encodingName))
                return null;

            if (int.TryParse(encodingName, out var codePage))
                return Encoding.GetEncoding(codePage);

            var encodingInfo = Encoding
                .GetEncodings()
                .FirstOrDefault(e => e.Name.Equals(encodingName, StringComparison.InvariantCultureIgnoreCase));

            if (encodingInfo == null)
                return null;

            return Encoding.GetEncoding(encodingInfo.CodePage);
        }
    }

    public class Example
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string DefaultLocation { get; set; }
        public string DefaultTimeZone { get; set; }
        public string? EncodingName { get; set; }
        public string ConfigText { get; set; }
        public string CsvText { get; set; }
    }
}
