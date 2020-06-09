using System;
using System.IO;
using System.Linq;
using Nett;

namespace TabularCsv
{
    public class SurveyLoader
    {
        public Survey Load(string path)
        {
            if (!File.Exists(path))
                throw new Exception($"'{path}' does not exist.");

            var text = File.ReadAllText(path);

            var survey = LoadFromToml(text);

            if (survey == null || IsEmpty(survey))
                return null;

            return survey;
        }

        private Survey LoadFromToml(string tomlText)
        {
            var settings = CreateTomlSettings();

            try
            {
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
                        .FromToml(ConvertFixedValueShorthandSyntax)))
            );

            return settings;
        }

        private PropertyDefinition ConvertFixedValueShorthandSyntax(ITomlRoot root, TomlString tomlString)
        {
            return new PropertyDefinition
            {
                FixedValue = tomlString.Value
            };
        }

        private static bool IsEmpty(Survey survey)
        {
            return survey.Name == null
                   && survey.Location == null
                   && !survey.Comments.Any()
                   && !survey.Party.Any()
                   && !survey.Readings.Any()
                   && !survey.Timestamps.Any();
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
