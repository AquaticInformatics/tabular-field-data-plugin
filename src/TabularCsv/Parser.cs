using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FieldDataPluginFramework;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.DataModel;
using FieldDataPluginFramework.DataModel.Calibrations;
using FieldDataPluginFramework.DataModel.ControlConditions;
using FieldDataPluginFramework.DataModel.DischargeActivities;
using FieldDataPluginFramework.DataModel.Inspections;
using FieldDataPluginFramework.DataModel.PickLists;
using FieldDataPluginFramework.DataModel.Readings;
using FieldDataPluginFramework.Results;
using NotVisualBasic.FileIO;

namespace TabularCsv
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
        private List<Configuration> Configurations { get; set; }
        private Configuration Configuration { get; set; }
        private long LineNumber { get; set; }
        private string[] Fields { get; set; }
        private List<string> HeaderLines { get; } = new List<string>();
        private Dictionary<string,string> HeaderRegexMatches { get; } = new Dictionary<string, string>();
        private Dictionary<string,int> ColumnHeaderMap { get; set; } = new Dictionary<string, int>();

        public ParseFileResult Parse(Stream stream, LocationInfo locationInfo = null)
        {
            var csvText = ReadTextFromStream(stream);

            if (csvText == null)
                return ParseFileResult.CannotParse();

            try
            {
                LocationInfo = locationInfo;

                Configurations = LoadConfigurations();

                using (ResultsAppender)
                {
                    foreach (var configuration in Configurations)
                    {
                        Configuration = configuration;
                        var result = ParseDataFile(csvText);

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

        private ParseFileResult ParseDataFile(string csvText)
        {
            var validator = new ConfigurationValidator
            {
                Configuration = Configuration
            };

            var (headerLines, dataRowReader) = ExtractHeaderLines(csvText);

            HeaderLines.AddRange(headerLines);

            BuildHeaderRegex();

            var dataRowCount = 0;

            using (var reader = dataRowReader)
            {
                var rowParser = GetCsvParser(reader);

                while(!rowParser.EndOfData)
                {
                    LineNumber = HeaderLines.Count + rowParser.LineNumber;

                    try
                    {
                        Fields = rowParser.ReadFields();
                    }
                    catch (Exception)
                    {
                        if (dataRowCount == 0)
                        {
                            // We'll hit this when the plugin tries to parse a text file that is not CSV, like a JSON document.
                            return ParseFileResult.CannotParse();
                        }
                    }

                    if (Fields == null) continue;

                    if (dataRowCount == 0 && Configuration.IsHeaderRowRequired)
                    {
                        try
                        {
                            ColumnHeaderMap = validator.BuildColumnHeaderHeaderMap(Fields);
                            ++dataRowCount;
                            continue;
                        }
                        catch (Exception exception)
                        {
                            // Most CSV files have a header.
                            // So a problem mapping the header is a strong indicator that this CSV file is not intended for us.
                            if (exception is AllHeadersMissingException)
                            {
                                // When all headers are missing, then we should exit without logging anything special.
                                // We'll just let the other plugins have a turn
                            }
                            else
                            {
                                // Some of the headers matched, so log a warning before returning CannotParse.
                                // This might be the only hint in the log that the configuration is incorrect.
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

                    ++dataRowCount;
                }

                return ParseFileResult.SuccessfullyParsedAndDataValid();
            }
        }

        private CsvTextFieldParser GetCsvParser(StringReader reader)
        {
            var delimiter = Configuration.Separator ?? ",";

            var rowParser = new CsvTextFieldParser(reader)
            {
                Delimiters = new[] {delimiter},
                TrimWhiteSpace = true,
                HasFieldsEnclosedInQuotes = true,
            };

            return rowParser;
        }

        private (IEnumerable<string> HeaderLines, StringReader RowReader) ExtractHeaderLines(string csvText)
        {
            var headerLines = new List<string>();

            if (!Configuration.IsHeaderSectionExpected)
                return (headerLines, new StringReader(csvText));

            using (var reader = new StringReader(csvText))
            {
                while (true)
                {
                    var line = reader.ReadLine();

                    if (line == null)
                        break;

                    if (!string.IsNullOrEmpty(Configuration.HeadersEndBefore) && line.StartsWith(Configuration.HeadersEndBefore))
                    {
                        // This line needs to be included in the start of the data section
                        var builder = new StringBuilder(line);
                        builder.AppendLine();
                        builder.Append(reader.ReadToEnd());

                        return (headerLines, new StringReader(builder.ToString()));
                    }

                    headerLines.Add(line);

                    if (Configuration.HeaderRowCount > 0 && headerLines.Count >= Configuration.HeaderRowCount)
                        break;

                    if (string.IsNullOrEmpty(Configuration.HeadersEndWith) && line.StartsWith(Configuration.HeadersEndWith))
                        break;
                }

                return (headerLines, new StringReader(reader.ReadToEnd()));
            }
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

        private void BuildHeaderRegex()
        {
            var regexColumns = Configuration
                .GetColumnDefinitions()
                .Where(column => column.HasHeaderRegex)
                .ToList();

            foreach (var regexColumn in regexColumns)
            {
                var regex = new Regex(regexColumn.HeaderRegex);

                foreach (var match in HeaderLines.Select(headerLine => regex.Match(headerLine)).Where(match => match.Success))
                {
                    HeaderRegexMatches[regexColumn.HeaderRegex] = match.Groups[ColumnDefinition.RegexCaptureGroupName].Value;
                }
            }
        }

        private List<Configuration> LoadConfigurations()
        {
            var configurationDirectory = GetConfigurationDirectory();

            if (!configurationDirectory.Exists)
            {
                Log.Error($"'{configurationDirectory.FullName}' does not exist. No configurations loaded.");
                return new List<Configuration>();
            }

            var configurationLoader = new ConfigurationLoader
            {
                Log = Log
            };

            var configurations = configurationDirectory
                .GetFiles("*.toml")
                .Select(fi => configurationLoader.Load(fi.FullName))
                .Where(s => s != null)
                .OrderBy(s => s.Priority)
                .ToList();

            if (!configurations.Any())
            {
                Log.Error($"No configurations found at '{configurationDirectory.FullName}\\*.toml'");
                return new List<Configuration>();
            }

            configurations = configurations
                .Where(s => LocationInfo != null || s.Location != null)
                .ToList();

            foreach (var configuration in configurations)
            {
                new ConfigurationValidator
                    {
                        LocationInfo = LocationInfo,
                        Configuration = configuration
                    }
                    .Validate();
            }

            return configurations;
        }

        private DirectoryInfo GetConfigurationDirectory()
        {
            return new DirectoryInfo(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                @"Aquatic Informatics\AQUARIUS Server\Configuration\TabularCSV"));
        }

        private void ParseRow()
        {
            var locationIdentifier = GetString(Configuration.Location);

            var locationInfo = LocationInfo ?? ResultsAppender.GetLocationByIdentifier(locationIdentifier);
            var comments = MergeTextColumns(Configuration.Comments);
            var party = MergeTextColumns(Configuration.Party);
            var timestamp = ParseTimestamp(locationInfo, Configuration.Timestamps);

            var readings = Configuration
                .Readings
                .Select(r => ParseReading(locationInfo, r))
                .Where(reading => reading != null)
                .Select(reading =>
                {
                    reading.DateTimeOffset = reading.DateTimeOffset ?? timestamp;
                    return reading;
                })
                .ToList();

            var inspections = Configuration
                .Inspections
                .Select(i => ParseInspection(locationInfo, i))
                .Where(inspection => inspection != null)
                .Select(inspection =>
                {
                    inspection.DateTimeOffset = inspection.DateTimeOffset ?? timestamp;
                    return inspection;
                })
                .ToList();

            var calibrations = Configuration
                .Calibrations
                .Select(c => ParseCalibration(locationInfo, c))
                .Where(calibration => calibration != null)
                .Select(calibration =>
                {
                    calibration.DateTimeOffset = calibration.DateTimeOffset ?? timestamp;
                    return calibration;
                })
                .ToList();

            var controlCondition = ParseControlCondition(locationInfo, Configuration.ControlCondition);

            var fieldVisitInfo = ResultsAppender.AddFieldVisit(locationInfo,
                new FieldVisitDetails(new DateTimeInterval(timestamp, TimeSpan.Zero))
                {
                    Comments = comments,
                    Party = party,
                    CollectionAgency = GetString(Configuration.CollectionAgency),
                    Weather = GetString(Configuration.Weather),
                    CompletedVisitActivities = ParseCompletedVisitActivities()
                });

            foreach (var reading in readings)
            {
                ResultsAppender.AddReading(fieldVisitInfo, reading);
            }

            foreach (var inspection in inspections)
            {
                ResultsAppender.AddInspection(fieldVisitInfo, inspection);
            }

            foreach (var calibration in calibrations)
            {
                ResultsAppender.AddCalibration(fieldVisitInfo, calibration);
            }

            if (controlCondition != null)
            {
                ResultsAppender.AddControlCondition(fieldVisitInfo, controlCondition);
            }
        }

        private CompletedVisitActivities ParseCompletedVisitActivities()
        {
            return new CompletedVisitActivities
            {
                GroundWaterLevels = GetNullableBoolean(Configuration.CompletedGroundWaterLevels) ?? false,
                ConductedLevelSurvey = GetNullableBoolean(Configuration.CompletedLevelSurvey) ?? false,
                RecorderDataCollected = GetNullableBoolean(Configuration.CompletedRecorderData) ?? false,
                SafetyInspectionPerformed = GetNullableBoolean(Configuration.CompletedSafetyInspection) ?? false,
                OtherSample = GetNullableBoolean(Configuration.CompletedOtherSample) ?? false,
                BiologicalSample = GetNullableBoolean(Configuration.CompletedBiologicalSample) ?? false,
                SedimentSample = GetNullableBoolean(Configuration.CompletedSedimentSample) ?? false,
                WaterQualitySample = GetNullableBoolean(Configuration.CompletedWaterQualitySample) ?? false,
            };
        }

        private string MergeTextColumns(List<MergingTextColumnDefinition> columns)
        {
            var lines = new List<string>();

            foreach (var column in columns)
            {
                var value = GetString(column);

                if (string.IsNullOrWhiteSpace(value)) continue;

                if (lines.Any(l => l.IndexOf(value, StringComparison.InvariantCultureIgnoreCase) >= 0)) continue;

                lines.Add($"{column.Prefix}{value}");
            }

            return string.Join("\n", lines);
        }

        private DateTimeOffset ParseTimestamp(LocationInfo locationInfo, List<TimestampColumnDefinition> timestampColumns)
        {
            var timestamp = new DateTimeOffset(new DateTime(1900,1,1), LocationInfo?.UtcOffset ?? locationInfo.UtcOffset);

            foreach (var timestampColumn in timestampColumns)
            {
                var timeText = GetString(timestampColumn);

                if (!DateTimeOffset.TryParseExact(timeText, timestampColumn.Format, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var value))
                    throw new Exception($"Line {LineNumber}: '{timeText}' can't be parsed as a timestamp using the '{timestampColumn.Format}' format.");

                if (!TimestampParsers.TryGetValue(timestampColumn.Type, out var timeParser))
                    throw new Exception($"{timestampColumn.Name()} Type={timestampColumn.Type} is not a supported time type");

                var utcOffset = GetNullableTimeSpan(timestampColumn.UtcOffset);

                timestamp = timeParser(utcOffset ?? LocationInfo?.UtcOffset, timestamp, value);
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

        private Reading ParseReading(LocationInfo locationInfo, ReadingColumnDefinition readingColumn)
        {
            var valueText = GetString(readingColumn);

            if (string.IsNullOrWhiteSpace(valueText))
                return null;

            DateTimeOffset? readingTime = null;

            if (readingColumn.Timestamps?.Any() ?? false)
            {
                readingTime = ParseTimestamp(locationInfo, readingColumn.Timestamps);
            }

            var readingValue = GetNullableDouble(readingColumn);

            if (!readingValue.HasValue)
                throw new InvalidOperationException("Should never happen");

            var reading = new Reading(
                GetString(readingColumn.ParameterId),
                new Measurement(readingValue.Value, GetString(readingColumn.UnitId)));

            reading.DateTimeOffset = readingTime;
            reading.Comments = GetString(readingColumn.Comments);
            reading.ReferencePointName = GetString(readingColumn.ReferencePointName);
            reading.SubLocation = GetString(readingColumn.SubLocation);
            reading.SensorUniqueId = GetNullableGuid(readingColumn.SensorUniqueId);
            reading.Uncertainty = GetNullableDouble(readingColumn.Uncertainty);

            if (!string.IsNullOrWhiteSpace(readingColumn.CommentPrefix))
            {
                reading.Comments = $"{readingColumn.CommentPrefix}{reading.Comments}";
            }

            var readingType = GetNullableEnum<ReadingType>(readingColumn.ReadingType);

            if (readingType.HasValue)
                reading.ReadingType = readingType.Value;

            var publish = GetNullableBoolean(readingColumn.Publish);
            var useLocationDatumAsReference = GetNullableBoolean(readingColumn.UseLocationDatumAsReference);

            var method = GetString(readingColumn.Method);

            if (!string.IsNullOrWhiteSpace(method))
                reading.Method = method;

            var gradeCode = GetNullableInteger(readingColumn.GradeCode);
            var gradeName = GetString(readingColumn.GradeName);

            if (gradeCode.HasValue)
                reading.Grade = Grade.FromCode(gradeCode.Value);

            if (!string.IsNullOrEmpty(gradeName))
                reading.Grade = Grade.FromDisplayName(gradeName);

            if (publish.HasValue)
                reading.Publish = publish.Value;

            if (useLocationDatumAsReference.HasValue)
                reading.UseLocationDatumAsReference = useLocationDatumAsReference.Value;

            var measurementDeviceManufacturer = GetString(readingColumn.MeasurementDeviceManufacturer);
            var measurementDeviceModel = GetString(readingColumn.MeasurementDeviceModel);
            var measurementDeviceSerialNumber = GetString(readingColumn.MeasurementDeviceSerialNumber);

            if (!string.IsNullOrEmpty(measurementDeviceManufacturer)
                || !string.IsNullOrEmpty(measurementDeviceModel)
                || !string.IsNullOrEmpty(measurementDeviceSerialNumber))
            {
                reading.MeasurementDevice = new MeasurementDevice(
                    measurementDeviceManufacturer,
                    measurementDeviceModel,
                    measurementDeviceSerialNumber);
            }

            var readingQualifierSeparators = GetString(readingColumn.ReadingQualifierSeparators);
            var readingQualifiers = GetString(readingColumn.ReadingQualifiers);

            if (!string.IsNullOrEmpty(readingQualifiers))
            {
                var qualifiers = new[] {readingQualifiers};

                if (!string.IsNullOrWhiteSpace(readingQualifierSeparators))
                {
                    qualifiers = readingQualifiers
                        .Split(readingQualifierSeparators.ToArray())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .ToArray();
                }

                reading.ReadingQualifiers = qualifiers
                    .Select(q => new ReadingQualifierPickList(q))
                    .ToList();
            }

            var measurementDetailsCut = GetNullableDouble(readingColumn.MeasurementDetailsCut);
            var measurementDetailsHold = GetNullableDouble(readingColumn.MeasurementDetailsHold);
            var measurementDetailsTapeCorrection = GetNullableDouble(readingColumn.MeasurementDetailsTapeCorrection);
            var measurementDetailsWaterLevel = GetNullableDouble(readingColumn.MeasurementDetailsWaterLevel);

            if (measurementDetailsCut.HasValue || measurementDetailsHold.HasValue || measurementDetailsTapeCorrection.HasValue ||
                measurementDetailsWaterLevel.HasValue)
            {
                reading.GroundWaterMeasurementDetails = new GroundWaterMeasurementDetails
                {
                    Cut = measurementDetailsCut,
                    Hold = measurementDetailsHold,
                    TapeCorrection = measurementDetailsTapeCorrection,
                    WaterLevel = measurementDetailsWaterLevel,
                };
            }

            return reading;
        }

        private Inspection ParseInspection(LocationInfo locationInfo, InspectionColumnDefinition inspectionColumn)
        {
            var inspectionType = GetNullableEnum<InspectionType>(inspectionColumn);

            if (!inspectionType.HasValue)
                return null;

            DateTimeOffset? inspectionTime = null;

            if (inspectionColumn.Timestamps?.Any() ?? false)
            {
                inspectionTime = ParseTimestamp(locationInfo, inspectionColumn.Timestamps);
            }

            var inspection = new Inspection(inspectionType.Value)
            {
                DateTimeOffset = inspectionTime,
                Comments = GetString(inspectionColumn.Comments),
                SubLocation = GetString(inspectionColumn.SubLocation)
            };

            var measurementDeviceManufacturer = GetString(inspectionColumn.MeasurementDeviceManufacturer);
            var measurementDeviceModel = GetString(inspectionColumn.MeasurementDeviceModel);
            var measurementDeviceSerialNumber = GetString(inspectionColumn.MeasurementDeviceSerialNumber);

            if (!string.IsNullOrEmpty(measurementDeviceManufacturer)
                || !string.IsNullOrEmpty(measurementDeviceModel)
                || !string.IsNullOrEmpty(measurementDeviceSerialNumber))
            {
                inspection.MeasurementDevice = new MeasurementDevice(
                    measurementDeviceManufacturer,
                    measurementDeviceModel,
                    measurementDeviceSerialNumber);
            }

            return inspection;
        }

        private Calibration ParseCalibration(LocationInfo locationInfo, CalibrationColumnDefinition calibrationColumn)
        {
            var valueText = GetString(calibrationColumn);

            if (string.IsNullOrWhiteSpace(valueText))
                return null;

            DateTimeOffset? calibrationTime = null;

            if (calibrationColumn.Timestamps?.Any() ?? false)
            {
                calibrationTime = ParseTimestamp(locationInfo, calibrationColumn.Timestamps);
            }

            var calibrationValue = GetNullableDouble(calibrationColumn);

            if (!calibrationValue.HasValue)
                throw new InvalidOperationException("Should never happen");

            var calibration = new Calibration(
                GetString(calibrationColumn.ParameterId),
                GetString(calibrationColumn.UnitId),
                calibrationValue.Value);

            calibration.DateTimeOffset = calibrationTime;
            calibration.Comments = GetString(calibrationColumn.Comments);
            calibration.Party = GetString(calibrationColumn.Party);
            calibration.SubLocation = GetString(calibrationColumn.SubLocation);
            calibration.SensorUniqueId = GetNullableGuid(calibrationColumn.SensorUniqueId);
            calibration.Standard = GetNullableDouble(calibrationColumn.Standard);

            var calibrationType = GetNullableEnum<CalibrationType>(calibrationColumn.CalibrationType);

            if (calibrationType.HasValue)
                calibration.CalibrationType = calibrationType.Value;

            var publish = GetNullableBoolean(calibrationColumn.Publish);

            var method = GetString(calibrationColumn.Method);

            if (!string.IsNullOrWhiteSpace(method))
                calibration.Method = method;

            if (publish.HasValue)
                calibration.Publish = publish.Value;

            var measurementDeviceManufacturer = GetString(calibrationColumn.MeasurementDeviceManufacturer);
            var measurementDeviceModel = GetString(calibrationColumn.MeasurementDeviceModel);
            var measurementDeviceSerialNumber = GetString(calibrationColumn.MeasurementDeviceSerialNumber);

            if (!string.IsNullOrEmpty(measurementDeviceManufacturer)
                || !string.IsNullOrEmpty(measurementDeviceModel)
                || !string.IsNullOrEmpty(measurementDeviceSerialNumber))
            {
                calibration.MeasurementDevice = new MeasurementDevice(
                    measurementDeviceManufacturer,
                    measurementDeviceModel,
                    measurementDeviceSerialNumber);
            }

            DateTimeOffset? expirationDate = null;

            if (calibrationColumn.StandardDetailsExpirationDate != null)
            {
                expirationDate = ParseTimestamp(locationInfo,
                    new List<TimestampColumnDefinition>
                    {
                        calibrationColumn.StandardDetailsExpirationDate
                    });
            }

            calibration.StandardDetails = new StandardDetails
            {
                LotNumber = GetString(calibrationColumn.StandardDetailsLotNumber),
                StandardCode = GetString(calibrationColumn.StandardDetailsStandardCode),
                ExpirationDate = expirationDate,
                Temperature = GetNullableDouble(calibrationColumn.StandardDetailsTemperature),
            };

            return calibration;
        }

        private ControlCondition ParseControlCondition(LocationInfo locationInfo, ControlConditionColumnDefinition controlConditionColumn)
        {
            if (controlConditionColumn == null)
                return null;

            DateTimeOffset? dateCleaned = null;

            if (controlConditionColumn.Timestamps?.Any() ?? false)
            {
                dateCleaned = ParseTimestamp(locationInfo, controlConditionColumn.Timestamps);
            }

            var conditionType = GetString(controlConditionColumn);
            var controlCode = GetString(controlConditionColumn.ControlCode);
            var controlCleanedType = GetNullableEnum<ControlCleanedType>(controlConditionColumn.ControlCleanedType);

            var controlCondition = new ControlCondition
            {
                Party = GetString(controlConditionColumn.Party),
                Comments = GetString(controlConditionColumn.Comments),
                DateCleaned = dateCleaned,
            };

            if (controlCleanedType.HasValue)
            {
                controlCondition.ControlCleaned = controlCleanedType.Value;
            }

            if (!string.IsNullOrWhiteSpace(controlCode))
            {
                controlCondition.ControlCode = new ControlCodePickList(controlCode);
            }

            if (!string.IsNullOrWhiteSpace(conditionType))
            {
                controlCondition.ConditionType = new ControlConditionPickList(conditionType);
            }

            var distanceToGage = GetNullableDouble(controlConditionColumn.DistanceToGage);

            var unitId = GetString(controlConditionColumn.UnitId);

            if (distanceToGage.HasValue)
            {
                controlCondition.DistanceToGage = new Measurement(distanceToGage.Value, unitId);
            }

            return controlCondition;
        }

        private bool? GetNullableBoolean(ColumnDefinition column)
        {
            var valueText = GetString(column);

            if (string.IsNullOrWhiteSpace(valueText))
                return null;

            if (bool.TryParse(valueText, out var value))
                return value;

            throw new ArgumentException($"Line {LineNumber} '{column.Name()}': '{valueText}' is an invalid boolean");
        }

        private TimeSpan? GetNullableTimeSpan(ColumnDefinition column)
        {
            var valueText = GetString(column);

            if (string.IsNullOrWhiteSpace(valueText))
                return null;

            if (TimeSpan.TryParse(valueText, CultureInfo.InvariantCulture, out var value))
                return value;

            throw new ArgumentException($"Line {LineNumber} '{column.Name()}': '{valueText}' is an invalid TimeSpan.");
        }

        private Guid? GetNullableGuid(ColumnDefinition column)
        {
            var valueText = GetString(column);

            if (string.IsNullOrWhiteSpace(valueText))
                return null;

            if (Guid.TryParse(valueText, out var value))
                return value;

            throw new ArgumentException($"Line {LineNumber} '{column.Name()}': '{valueText}' is an invalid Guid.");
        }

        private int? GetNullableInteger(ColumnDefinition column)
        {
            var valueText = GetString(column);

            if (string.IsNullOrWhiteSpace(valueText))
                return null;

            if (int.TryParse(valueText, out var value))
                return value;

            throw new ArgumentException($"Line {LineNumber} '{column.Name()}': '{valueText}' is an invalid integer.");
        }

        private double? GetNullableDouble(ColumnDefinition column)
        {
            var valueText = GetString(column);

            if (string.IsNullOrWhiteSpace(valueText))
                return null;

            if (double.TryParse(valueText, NumberStyles.Any, CultureInfo.InvariantCulture, out var value))
                return value;

            throw new ArgumentException($"Line {LineNumber} '{column.Name()}': '{valueText}' is an invalid number.");
        }

        private TEnum? GetNullableEnum<TEnum>(ColumnDefinition column) where TEnum : struct
        {
            var text = GetString(column);

            if (string.IsNullOrWhiteSpace(text))
                return null;

            if (Enum.TryParse<TEnum>(text, true, out var enumValue))
                return enumValue;

            throw new ArgumentException($"Line {LineNumber} '{column.Name()}': '{text}' is not a valid {typeof(TEnum).Name} value. Supported values are: {string.Join(", ", Enum.GetNames(typeof(TEnum)))}");
        }

        private string GetString(ColumnDefinition column)
        {
            if (column == null)
                return null;

            return GetColumnValue(column);
        }

        private string GetColumnValue(ColumnDefinition column)
        {
            if (column.HasHeaderRegex)
                return HeaderRegexMatches.TryGetValue(column.HeaderRegex, out var value)
                    ? value
                    : null;

            if (column.HasFixedValue)
                return column.FixedValue;

            var fieldIndex = column.RequiresColumnHeader()
                ? ColumnHeaderMap[column.ColumnHeader]
                : column.ColumnIndex ?? 0;

            if (fieldIndex <= 0 || fieldIndex > Fields.Length)
                throw new ArgumentException($"Line {LineNumber} '{column.Name()}' has an invalid index={fieldIndex}.");

            return Fields[fieldIndex - 1];
        }
    }
}
