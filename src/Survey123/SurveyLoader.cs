using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using ServiceStack;

namespace Survey123
{
    public class SurveyLoader
    {
        public Survey Load(string path)
        {
            if (!File.Exists(path))
                throw new Exception($"'{path}' does not exist.");

            Survey survey;

            try
            {
                survey = File.ReadAllText(path).FromJson<Survey>();
            }
            catch (SerializationException)
            {
                return null;
            }

            if (survey == null || IsEmpty(survey))
                return null;

            // Ensure that the alias dictionary is case insensitive
            survey.LocationAliases = new Dictionary<string, string>(survey.LocationAliases, StringComparer.InvariantCultureIgnoreCase);

            return survey;
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

        public Survey CreateDefaultSurvey()
        {
            return new Survey
            {
                Name = "IDWR field data",
                FirstLineIsHeader = true,
                LocationColumn = new ColumnDefinition
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
    }
}
