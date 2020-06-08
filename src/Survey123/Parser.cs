﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualBasic.FileIO;
using FieldDataPluginFramework;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.DataModel;
using FieldDataPluginFramework.DataModel.Readings;
using FieldDataPluginFramework.Results;

namespace Survey123
{
    public class Parser
    {
        public Parser(ILog logger, IFieldDataResultsAppender resultsAppender)
        {
            Log = logger;
            ResultsAppender = new DelayedAppender(resultsAppender);
        }

        private ILog Log { get; }
        private DelayedAppender ResultsAppender { get; }
        private LocationInfo LocationInfo { get; set; }
        private List<Survey> Surveys { get; set; }
        private Survey Survey { get; set; }
        private long LineNumber { get; set; }
        private string[] Fields { get; set; }
        private Dictionary<string,int> HeaderMap { get; set; } = new Dictionary<string, int>();

        public ParseFileResult Parse(Stream stream, LocationInfo locationInfo = null)
        {
            var csvText = ReadTextFromStream(stream);

            if (csvText == null)
                return ParseFileResult.CannotParse();

            try
            {
                LocationInfo = locationInfo;

                Surveys = LoadSurveys();

                using (ResultsAppender)
                {
                    foreach (var survey in Surveys)
                    {
                        Survey = survey;
                        var result = ParseSurvey(csvText);

                        if (result.Status == ParseFileStatus.CannotParse) continue;

                        return result;
                    }

                    return ParseFileResult.CannotParse();
                }
            }
            catch (Exception exception)
            {
                return ParseFileResult.SuccessfullyParsedButDataInvalid(exception);
            }
        }

        private ParseFileResult ParseSurvey(string csvText)
        {
            var validator = new SurveyValidator {Survey = Survey};

            var lineCount = 0;

            using (var reader = new StringReader(csvText))
            {
                var rowParser = GetCsvParser(reader);

                for (; !rowParser.EndOfData; ++lineCount)
                {
                    LineNumber = rowParser.LineNumber;

                    try
                    {
                        Fields = rowParser.ReadFields();
                    }
                    catch (Exception)
                    {
                        if (lineCount == 0)
                        {
                            // We'll hit this when the plugin tries to parse a text file that is not CSV, like a JSON document.
                            return ParseFileResult.CannotParse();
                        }
                    }

                    if (Fields == null) continue;

                    if (lineCount == 0 && Survey.FirstLineIsHeader)
                    {
                        try
                        {
                            HeaderMap = validator.BuildHeaderMap(Fields);
                            continue;
                        }
                        catch (Exception exception)
                        {
                            // Most Survey123 files have a header.
                            // So a problem mapping the header is a strong indicator that this CSV file is not intended for us.
                            if (exception is AllHeadersMissingException)
                            {
                                // When all headers are missing, then we should exit without logging anything special.
                                // We'll just let the other plugins have a turn
                            }
                            else
                            {
                                // Some of the headers matched, so log a warning before returning CannotParse.
                                // This might be the only hint in the log that the survey configuration JSON is incorrect.
                                Log.Info($"Partial headers detected: {exception.Message}");
                            }

                            return ParseFileResult.CannotParse();
                        }
                    }

                    try
                    {
                        ParseRow();
                    }
                    catch (Exception exception)
                    {
                        if (!ResultsAppender.AnyResultsAppended) throw;

                        Log.Error($"Ignoring invalid CSV row: {exception.Message}");
                    }
                }

                return ParseFileResult.SuccessfullyParsedAndDataValid();
            }
        }

        private static TextFieldParser GetCsvParser(StringReader reader)
        {
            var rowParser = new TextFieldParser(reader)
            {
                TextFieldType = FieldType.Delimited,
                Delimiters = new[] {","},
                TrimWhiteSpace = true,
                HasFieldsEnclosedInQuotes = true,
            };
            return rowParser;
        }

        private string ReadTextFromStream(Stream stream)
        {
            try
            {
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private List<Survey> LoadSurveys()
        {
            var surveyDirectory = new DirectoryInfo(GetPluginDirectory());

            var surveyLoader = new SurveyLoader();

            var surveys = surveyDirectory
                .GetFiles("*.toml")
                .Select(fi => surveyLoader.Load(fi.FullName))
                .Where(s => s != null)
                .ToList();

            if (!surveys.Any())
                throw new Exception($"No survey definitions found at '{surveyDirectory.FullName}\\*.json'");

            surveys = surveys
                .Where(s => LocationInfo != null || s.LocationColumn != null)
                .ToList();

            foreach (var survey in surveys)
            {
                new SurveyValidator
                    {
                        LocationInfo = LocationInfo,
                        Survey = survey
                    }
                    .Validate();
            }

            return surveys;
        }

        private string GetPluginDirectory()
        {
            return Path.GetDirectoryName(GetType().Assembly.Location);
        }

        private void ParseRow()
        {
            var locationIdentifier = GetColumnValue(Survey.LocationColumn);

            if (Survey.LocationAliases.TryGetValue(locationIdentifier, out var aliasedIdentifier))
            {
                locationIdentifier = aliasedIdentifier;
            }

            var locationInfo = LocationInfo ?? ResultsAppender.GetLocationByIdentifier(locationIdentifier);
            var comments = MergeTextColumns(Survey.CommentColumns);
            var party = MergeTextColumns(Survey.PartyColumns);
            var timestamp = ParseTimestamp(locationInfo);

            var readings = Survey
                .ReadingColumns
                .Select(ParseReading)
                .Where(r => r != null)
                .Select(r =>
                {
                    r.DateTimeOffset = timestamp;
                    return r;
                })
                .ToList();

            var fieldVisitInfo = ResultsAppender.AddFieldVisit(locationInfo,
                new FieldVisitDetails(new DateTimeInterval(timestamp, TimeSpan.Zero))
                {
                    Comments = comments,
                    Party = party
                });

            foreach (var reading in readings)
            {
                ResultsAppender.AddReading(fieldVisitInfo, reading);
            }
        }

        private string MergeTextColumns(List<MergingTextColumnDefinition> columns)
        {
            var lines = new List<string>();

            foreach (var column in columns)
            {
                var value = GetColumnValue(column);

                if (string.IsNullOrWhiteSpace(value)) continue;

                if (lines.Any(l => l.IndexOf(value, StringComparison.InvariantCultureIgnoreCase) >= 0)) continue;

                lines.Add($"{column.Prefix}{value}");
            }

            return string.Join("\n", lines);
        }

        private DateTimeOffset ParseTimestamp(LocationInfo locationInfo)
        {
            var timestamp = new DateTimeOffset(new DateTime(1900,1,1), LocationInfo?.UtcOffset ?? locationInfo.UtcOffset);

            foreach (var timestampColumn in Survey.TimestampColumns)
            {
                var timeText = GetColumnValue(timestampColumn);

                if (!DateTimeOffset.TryParseExact(timeText, timestampColumn.Format, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var value))
                    throw new Exception($"Line {LineNumber}: '{timeText}' can't be parsed as a timestamp using the '{timestampColumn.Format}' format.");

                if (!TimestampParsers.TryGetValue(timestampColumn.Type, out var timeParser))
                    throw new Exception($"{timestampColumn.Name()} Type={timestampColumn.Type} is not a supported time type");

                timestamp = timeParser(timestampColumn.UtcOffset, timestamp, value);
            }

            return timestamp;
        }

        private static readonly Dictionary<TimestampType, Func<TimeSpan?, DateTimeOffset, DateTimeOffset, DateTimeOffset>>
            TimestampParsers =
                new Dictionary<TimestampType, Func<TimeSpan?, DateTimeOffset, DateTimeOffset, DateTimeOffset>>
                {
                    {TimestampType.TimeOnly, MergeTime},
                    {TimestampType.DateOnly, MergeDate},
                    {TimestampType.DateTimeOnly, MergeDateTime},
                    {TimestampType.DateTimeOffset, ReplaceDateTimeOffset},
                    {TimestampType.DateAndSurvey123Offset, MergeDateAndSurvey123Offset},
                };

        private static DateTimeOffset MergeTime(TimeSpan? utcOffset, DateTimeOffset existing, DateTimeOffset value)
        {
            return new DateTimeOffset(
                    existing.Date,
                    existing.Offset)
                .Add(value.TimeOfDay);
        }

        private static DateTimeOffset MergeDate(TimeSpan? utcOffset, DateTimeOffset existing, DateTimeOffset value)
        {
            return new DateTimeOffset(
                existing.Date,
                existing.Offset)
                .Add(value.TimeOfDay);
        }

        private static DateTimeOffset MergeDateTime(TimeSpan? utcOffset, DateTimeOffset existing, DateTimeOffset value)
        {
            return new DateTimeOffset(value.DateTime, existing.Offset);
        }

        private static DateTimeOffset ReplaceDateTimeOffset(TimeSpan? utcOffset, DateTimeOffset existing, DateTimeOffset value)
        {
            return value;
        }

        private static DateTimeOffset MergeDateAndSurvey123Offset(TimeSpan? utcOffset, DateTimeOffset existing, DateTimeOffset value)
        {
            return new DateTimeOffset(
                    value.Date,
                    utcOffset ?? value.TimeOfDay.Negate())
                .Add(existing.TimeOfDay);
        }

        private Reading ParseReading(ReadingColumnDefinition readingColumn)
        {
            var valueText = GetColumnValue(readingColumn);

            if (string.IsNullOrWhiteSpace(valueText))
                return null;

            if (!double.TryParse(valueText, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                throw new Exception($"Line {LineNumber}: '{valueText}' is an invalid number for '{readingColumn.Name()}'");

            var reading = new Reading(readingColumn.ParameterId, new Measurement(value, readingColumn.UnitId));

            if (!string.IsNullOrWhiteSpace(readingColumn.CommentPrefix))
            {
                reading.Comments = readingColumn.CommentPrefix;
            }

            reading.ReadingType = readingColumn.ReadingType ?? ReadingType.Routine;

            // TODO: Support other reading properties like uncertainty, device, sublocation, etc.
            return reading;
        }

        private string GetColumnValue(ColumnDefinition column)
        {
            var fieldIndex = column.RequiresHeader()
                ? HeaderMap[column.ColumnHeader]
                : column.ColumnIndex ?? 0;

            if (fieldIndex <= 0 || fieldIndex > Fields.Length)
                throw new ArgumentOutOfRangeException($"Line {LineNumber}: '{column.Name()}' has an invalid index={fieldIndex}.");

            return Fields[fieldIndex - 1];
        }
    }
}
