using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using Nett;
using ServiceStack;

namespace TabularCsv
{
    public class SurveyLoader
    {
        public Survey Load(string path)
        {
            if (!File.Exists(path))
                throw new Exception($"'{path}' does not exist.");

            var text = File.ReadAllText(path);

            var survey = path.EndsWith(".toml", StringComparison.InvariantCultureIgnoreCase)
                ? LoadFromToml(text)
                : LoadFromJson(text);

            if (survey == null || IsEmpty(survey))
                return null;

            // Ensure that the alias dictionary is case insensitive
            survey.LocationAliases = new Dictionary<string, string>(survey.LocationAliases, StringComparer.InvariantCultureIgnoreCase);

            return survey;
        }

        private Survey LoadFromJson(string jsonText)
        {
            try
            {
                return jsonText.FromJson<Survey>();
            }
            catch (SerializationException)
            {
                return null;
            }
        }

        private Survey LoadFromToml(string tomlText)
        {
            var settings = CreateTomlSettings();

            try
            {
                var x1 = Toml.ReadString<Survey>(@"[LocationColumn]
ColumnHeader = 'fred'", settings);
                var x2 = Toml.ReadString<Survey>("LocationColumn = 'fred'", settings);
                var x3 = Toml.ReadString<Survey>("LocationColumn = { ColumnHeader = 'Thinger'}", settings);
                return Toml.ReadString<Survey>(tomlText, settings);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private TomlSettings CreateTomlSettings()
        {
            var settings = TomlSettings.Create(s => s
                .ConfigurePropertyMapping(m => m
                    .UseTargetPropertySelector(standardSelectors => standardSelectors.IgnoreCase))
                .ConfigureType<PropertyDefinition>(type => type
                    .WithConversionFor<TomlString>(convert => convert
                        .ToToml(ToToml)
                        .FromToml(FromToml)))
            );

            return settings;
        }

        private string ToToml(PropertyDefinition propertyDefinition)
        {
            if (!string.IsNullOrEmpty(propertyDefinition.FixedValue))
                return $"\"{propertyDefinition.FixedValue}\"";

            var builder = new StringBuilder("{");

            if (propertyDefinition.ColumnIndex.HasValue)
                builder.Append($" {nameof(propertyDefinition.ColumnIndex)} = {propertyDefinition.ColumnIndex}");

            if (!string.IsNullOrEmpty(propertyDefinition.ColumnHeader))
                builder.Append($" {nameof(propertyDefinition.ColumnHeader)} = \"{propertyDefinition.ColumnHeader}\"");

            builder.Append(" }");

            return builder.ToString();
        }

        private PropertyDefinition FromToml(ITomlRoot root, TomlString tomlString)
        {
            return tomlString.TomlType == TomlObjectType.String
                ? new PropertyDefinition
                {
                    FixedValue = tomlString.Value
                }
                : new PropertyDefinition
                {

                };
        }

        private static bool IsEmpty(Survey survey)
        {
            return survey.Name == null
                   && survey.LocationColumn == null
                   && !survey.LocationAliases.Any()
                   && !survey.CommentColumns.Any()
                   && !survey.PartyColumns.Any()
                   && !survey.ReadingColumns.Any()
                   && !survey.TimestampColumns.Any();
        }

        /*
        public Survey CreateDefaultSurvey()
        {
            return new Survey
            {
                Name = "IDWR field data",
                FirstLineIsHeader = true,
                LocationColumn = new PropertyDefinition
                {
                    ColumnHeader = "Please type in the site name:"
                },
                CommentColumns = new List<MergingTextColumnDefinition>
                {
                    new MergingTextColumnDefinition
                    {
                        ColumnHeader = "Comments:",
                    },
                    new MergingTextColumnDefinition
                    {
                        ColumnHeader = "Maintenance Issues:",
                        Prefix = "Maintenance Issues: "
                    },
                },
                PartyColumns = new List<MergingTextColumnDefinition>
                {
                    new MergingTextColumnDefinition
                    {
                        ColumnHeader = "Examiner:"
                    },
                },
                TimestampColumns = new List<TimestampColumnDefinition>
                {
                    new TimestampColumnDefinition
                    {
                        ColumnHeader = "Visit Date:",
                        Format = "M/d/yyyy h:m:s tt",
                        Type = TimestampType.DateAndSurvey123Offset
                    },
                    new TimestampColumnDefinition
                    {
                        ColumnHeader = "Visit Time:",
                        Format = "H:m",
                        Type = TimestampType.TimeOnly
                    },
                },
                ReadingColumns = new List<ReadingColumnDefinition>
                {
                    new ReadingColumnDefinition
                    {
                        ColumnHeader = "Staff Gage Reading 1 (ft):",
                        CommentPrefix = "#1 Staff Gage",
                        ParameterId = "HG",
                        UnitId = "ft",
                    },
                    new ReadingColumnDefinition
                    {
                        ColumnHeader = "Staff Gage Reading 2 (ft):",
                        CommentPrefix = "#2 Staff Gage",
                        ParameterId = "HG",
                        UnitId = "ft",
                    },
                    new ReadingColumnDefinition
                    {
                        ColumnHeader = "Flow Meter Reading 1 (cfs):",
                        CommentPrefix = "#1 Flow Meter",
                        ParameterId = "QR",
                        UnitId = "ft^3/s",
                    },
                    new ReadingColumnDefinition
                    {
                        ColumnHeader = "Flow Meter Reading 2 (cfs):",
                        CommentPrefix = "#2 Flow Meter",
                        ParameterId = "QR",
                        UnitId = "ft^3/s",
                    },
                    new ReadingColumnDefinition
                    {
                        ColumnHeader = "Discharge Measured in CFS",
                        CommentPrefix = "Measured discharge",
                        ParameterId = "QR",
                        UnitId = "ft^3/s",
                    },
                },
                LocationAliases = new Dictionary<string, string>
                {
                    { "SurveyId", "LocationIdentifier" },
                    { "LoggerSiteId", "AqtsLocationIdentifier" },
                }
            };
        }
        */
    }
}
