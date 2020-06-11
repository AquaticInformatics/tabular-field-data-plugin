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
using FieldDataPluginFramework.DataModel.ChannelMeasurements;
using FieldDataPluginFramework.DataModel.ControlConditions;
using FieldDataPluginFramework.DataModel.DischargeActivities;
using FieldDataPluginFramework.DataModel.Inspections;
using FieldDataPluginFramework.DataModel.Meters;
using FieldDataPluginFramework.DataModel.PickLists;
using FieldDataPluginFramework.DataModel.Readings;
using FieldDataPluginFramework.DataModel.Verticals;
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

                    if (Fields == null)
                        continue;

                    if (Fields.Length > 0 && !string.IsNullOrEmpty(Configuration.CommentLinePrefix) && Fields[0].StartsWith(Configuration.CommentLinePrefix))
                        continue;

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

            var fieldVisitInfo = DelayedAppender.InternalConstructor<FieldVisitInfo>.Invoke(
                locationInfo,
                ParseVisit(locationInfo));

            var readings = Configuration
                .AllReadings
                .Select(r => ParseReading(fieldVisitInfo, r))
                .Where(reading => reading != null)
                .ToList();

            var inspections = Configuration
                .AllInspections
                .Select(i => ParseInspection(fieldVisitInfo, i))
                .Where(inspection => inspection != null)
                .ToList();

            var calibrations = Configuration
                .AllCalibrations
                .Select(c => ParseCalibration(fieldVisitInfo, c))
                .Where(calibration => calibration != null)
                .ToList();

            var controlCondition = ParseControlCondition(fieldVisitInfo, Configuration.ControlCondition);

            var discharges = Configuration
                .AllAdcpDischarges
                .Select(adcp => ParseAdcpDischarge(fieldVisitInfo, adcp))
                .Concat(Configuration
                    .AllPanelDischargeSummaries
                    .Select(panel => ParsePanelSectionDischarge(fieldVisitInfo, panel)))
                .Where(discharge => discharge != null)
                .ToList();

            fieldVisitInfo = ResultsAppender.AddFieldVisit(locationInfo, fieldVisitInfo.FieldVisitDetails);

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

            foreach (var discharge in discharges)
            {
                ResultsAppender.AddDischargeActivity(fieldVisitInfo, discharge);
            }
        }

        private FieldVisitDetails ParseVisit(LocationInfo locationInfo)
        {
            var visit = Configuration.Visit;

            var fieldVisitPeriod = ParseInterval(
                locationInfo,
                visit.AllTimes,
                visit.AllStartTimes,
                visit.AllEndTimes)
                ?? ParseInterval(
                    locationInfo,
                    Configuration.AllTimes,
                    Configuration.AllStartTimes,
                    Configuration.AllEndTimes);

            if (fieldVisitPeriod == null)
            {
                var allTimeColumns = Configuration.AllTimes
                    .Concat(Configuration.AllStartTimes)
                    .Concat(Configuration.AllEndTimes)
                    .Concat(visit.AllTimes)
                    .Concat(visit.AllStartTimes)
                    .Concat(visit.AllEndTimes)
                    .ToList();

                if (!allTimeColumns.Any())
                    throw new Exception($"Line {LineNumber}: No timestamp columns are configured.");

                throw new Exception($"Line {LineNumber}: No timestamp could be calculated from these columns: {string.Join(", ", allTimeColumns.Select(c => c.Name()))}");
            }

            return new FieldVisitDetails(fieldVisitPeriod)
            {
                Comments = MergeTextColumns(visit.Comments),
                Party = MergeTextColumns(visit.Party),
                CollectionAgency = GetString(visit.CollectionAgency),
                Weather = GetString(visit.Weather),
                CompletedVisitActivities = ParseCompletedVisitActivities(visit)
            };
        }

        private CompletedVisitActivities ParseCompletedVisitActivities(VisitDefinition visit)
        {
            return new CompletedVisitActivities
            {
                GroundWaterLevels = GetNullableBoolean(visit.CompletedGroundWaterLevels) ?? false,
                ConductedLevelSurvey = GetNullableBoolean(visit.CompletedLevelSurvey) ?? false,
                RecorderDataCollected = GetNullableBoolean(visit.CompletedRecorderData) ?? false,
                SafetyInspectionPerformed = GetNullableBoolean(visit.CompletedSafetyInspection) ?? false,
                OtherSample = GetNullableBoolean(visit.CompletedOtherSample) ?? false,
                BiologicalSample = GetNullableBoolean(visit.CompletedBiologicalSample) ?? false,
                SedimentSample = GetNullableBoolean(visit.CompletedSedimentSample) ?? false,
                WaterQualitySample = GetNullableBoolean(visit.CompletedWaterQualitySample) ?? false,
            };
        }

        private string MergeTextColumns(List<MergingTextDefinition> columns)
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

        private DateTimeInterval ParseInterval(LocationInfo locationInfo, List<TimestampDefinition> timestampColumns, List<TimestampDefinition> startColumns, List<TimestampDefinition> endColumns)
        {
            var time = ParseNullableDateTimeOffset(locationInfo, timestampColumns);
            var startTime = ParseNullableDateTimeOffset(locationInfo, startColumns);
            var endTime = ParseNullableDateTimeOffset(locationInfo, endColumns);

            if (!time.HasValue && !startTime.HasValue && !endTime.HasValue)
                return null;

            if (!startTime.HasValue && !endTime.HasValue)
                return new DateTimeInterval(time.Value, TimeSpan.Zero);

            // ReSharper disable once ConstantNullCoalescingCondition
            var start = startTime ?? endTime ?? DateTimeOffset.MaxValue;
            // ReSharper disable once ConstantNullCoalescingCondition
            var end = endTime ?? startTime ?? DateTimeOffset.MinValue;

            return new DateTimeInterval(start, end);
        }

        private DateTimeOffset ParseDateTimeOffset(LocationInfo locationInfo, List<TimestampDefinition> timestampColumns)
        {
            var dateTimeOffset = ParseNullableDateTimeOffset(locationInfo, timestampColumns);

            if (!dateTimeOffset.HasValue)
            {
                throw new Exception($"Line {LineNumber}: No timestamp columns are configured. Can't figure out when this activity exists.");
            }

            return dateTimeOffset.Value;
        }

        private DateTimeOffset? ParseNullableDateTimeOffset(LocationInfo locationInfo, List<TimestampDefinition> timestampColumns)
        {
            if (!timestampColumns.Any())
                return null;

            var timestamp = new DateTimeOffset(new DateTime(1900, 1, 1), LocationInfo?.UtcOffset ?? locationInfo.UtcOffset);

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

        private DateTimeOffset ParseActivityTime(FieldVisitInfo visitInfo, ActivityDefinition activity, DateTimeOffset? fallbackTime = null)
        {
            var time = ParseNullableDateTimeOffset(visitInfo.LocationInfo, activity.AllTimes);

            return time ?? fallbackTime ?? visitInfo.StartDate;
        }

        private DateTimeInterval ParseActivityTimeRange(FieldVisitInfo visitInfo, TimeRangeActivityDefinition timeRangeActivity)
        {
            return ParseInterval(
                       visitInfo.LocationInfo,
                       timeRangeActivity.AllTimes,
                       timeRangeActivity.AllStartTimes,
                       timeRangeActivity.AllEndTimes)
                   ?? visitInfo.FieldVisitDetails.FieldVisitPeriod;
        }

        private Reading ParseReading(FieldVisitInfo visitInfo, ReadingDefinition readingDefinition)
        {
            var valueText = GetString(readingDefinition);

            if (string.IsNullOrWhiteSpace(valueText))
                return null;

            var readingValue = GetDouble(readingDefinition);

            var reading = new Reading(
                GetString(readingDefinition.ParameterId),
                new Measurement(readingValue, GetString(readingDefinition.UnitId)))
            {
                DateTimeOffset = ParseActivityTime(visitInfo, readingDefinition),
                Comments = GetString(readingDefinition.Comments),
                ReferencePointName = GetString(readingDefinition.ReferencePointName),
                SubLocation = GetString(readingDefinition.SubLocation),
                SensorUniqueId = GetNullableGuid(readingDefinition.SensorUniqueId),
                Uncertainty = GetNullableDouble(readingDefinition.Uncertainty),
                MeasurementDevice = ParseMeasurementDevice(
                    readingDefinition.MeasurementDeviceManufacturer,
                    readingDefinition.MeasurementDeviceModel,
                    readingDefinition.MeasurementDeviceSerialNumber)
            };

            if (!string.IsNullOrWhiteSpace(readingDefinition.CommentPrefix))
            {
                reading.Comments = $"{readingDefinition.CommentPrefix}{reading.Comments}";
            }

            var readingType = GetNullableEnum<ReadingType>(readingDefinition.ReadingType);

            if (readingType.HasValue)
                reading.ReadingType = readingType.Value;

            var publish = GetNullableBoolean(readingDefinition.Publish);
            var useLocationDatumAsReference = GetNullableBoolean(readingDefinition.UseLocationDatumAsReference);

            var method = GetString(readingDefinition.Method);

            if (!string.IsNullOrWhiteSpace(method))
                reading.Method = method;

            var gradeCode = GetNullableInteger(readingDefinition.GradeCode);
            var gradeName = GetString(readingDefinition.GradeName);

            if (gradeCode.HasValue)
                reading.Grade = Grade.FromCode(gradeCode.Value);

            if (!string.IsNullOrEmpty(gradeName))
                reading.Grade = Grade.FromDisplayName(gradeName);

            if (publish.HasValue)
                reading.Publish = publish.Value;

            if (useLocationDatumAsReference.HasValue)
                reading.UseLocationDatumAsReference = useLocationDatumAsReference.Value;

            var readingQualifierSeparators = GetString(readingDefinition.ReadingQualifierSeparators);
            var readingQualifiers = GetString(readingDefinition.ReadingQualifiers);

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

            var measurementDetailsCut = GetNullableDouble(readingDefinition.MeasurementDetailsCut);
            var measurementDetailsHold = GetNullableDouble(readingDefinition.MeasurementDetailsHold);
            var measurementDetailsTapeCorrection = GetNullableDouble(readingDefinition.MeasurementDetailsTapeCorrection);
            var measurementDetailsWaterLevel = GetNullableDouble(readingDefinition.MeasurementDetailsWaterLevel);

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

        private MeasurementDevice ParseMeasurementDevice(
            PropertyDefinition manufacturerDefinition,
            PropertyDefinition modelDefinition,
            PropertyDefinition serialNumberDefinition)
        {
            var manufacturer = GetString(manufacturerDefinition);
            var model = GetString(modelDefinition);
            var serialNumber = GetString(serialNumberDefinition);

            if (!string.IsNullOrEmpty(manufacturer)
                || !string.IsNullOrEmpty(model)
                || !string.IsNullOrEmpty(serialNumber))
            {
                return new MeasurementDevice(
                    manufacturer,
                    model,
                    serialNumber);
            }

            return null;
        }

        private Inspection ParseInspection(FieldVisitInfo visitInfo, InspectionDefinition inspectionDefinition)
        {
            var inspectionType = GetNullableEnum<InspectionType>(inspectionDefinition);

            if (!inspectionType.HasValue)
                return null;

            var inspection = new Inspection(inspectionType.Value)
            {
                DateTimeOffset = ParseActivityTime(visitInfo, inspectionDefinition),
                Comments = GetString(inspectionDefinition.Comments),
                SubLocation = GetString(inspectionDefinition.SubLocation),
                MeasurementDevice = ParseMeasurementDevice(
                    inspectionDefinition.MeasurementDeviceManufacturer,
                    inspectionDefinition.MeasurementDeviceModel,
                    inspectionDefinition.MeasurementDeviceSerialNumber)
            };

            return inspection;
        }

        private Calibration ParseCalibration(FieldVisitInfo visitInfo, CalibrationDefinition calibrationDefinition)
        {
            var valueText = GetString(calibrationDefinition);

            if (string.IsNullOrWhiteSpace(valueText))
                return null;

            var calibrationValue = GetDouble(calibrationDefinition);

            var calibration = new Calibration(
                GetString(calibrationDefinition.ParameterId),
                GetString(calibrationDefinition.UnitId),
                calibrationValue)
            {
                DateTimeOffset = ParseActivityTime(visitInfo, calibrationDefinition),
                Comments = GetString(calibrationDefinition.Comments),
                Party = GetString(calibrationDefinition.Party),
                SubLocation = GetString(calibrationDefinition.SubLocation),
                SensorUniqueId = GetNullableGuid(calibrationDefinition.SensorUniqueId),
                Standard = GetNullableDouble(calibrationDefinition.Standard),
                MeasurementDevice = ParseMeasurementDevice(
                    calibrationDefinition.MeasurementDeviceManufacturer,
                    calibrationDefinition.MeasurementDeviceModel,
                    calibrationDefinition.MeasurementDeviceSerialNumber)
            };

            var calibrationType = GetNullableEnum<CalibrationType>(calibrationDefinition.CalibrationType);

            if (calibrationType.HasValue)
                calibration.CalibrationType = calibrationType.Value;

            var publish = GetNullableBoolean(calibrationDefinition.Publish);

            var method = GetString(calibrationDefinition.Method);

            if (!string.IsNullOrWhiteSpace(method))
                calibration.Method = method;

            if (publish.HasValue)
                calibration.Publish = publish.Value;

            DateTimeOffset? expirationDate = null;

            if (calibrationDefinition.StandardDetailsExpirationDate != null)
            {
                expirationDate = ParseDateTimeOffset(visitInfo.LocationInfo,
                    new List<TimestampDefinition>
                    {
                        calibrationDefinition.StandardDetailsExpirationDate
                    });
            }

            calibration.StandardDetails = new StandardDetails
            {
                LotNumber = GetString(calibrationDefinition.StandardDetailsLotNumber),
                StandardCode = GetString(calibrationDefinition.StandardDetailsStandardCode),
                ExpirationDate = expirationDate,
                Temperature = GetNullableDouble(calibrationDefinition.StandardDetailsTemperature),
            };

            return calibration;
        }

        private ControlCondition ParseControlCondition(FieldVisitInfo visitInfo, ControlConditionColumnDefinition controlConditionColumn)
        {
            if (controlConditionColumn == null)
                return null;

            DateTimeOffset? dateCleaned = null;

            if (controlConditionColumn.AllTimes?.Any() ?? false)
            {
                dateCleaned = ParseDateTimeOffset(visitInfo.LocationInfo, controlConditionColumn.AllTimes);
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

        private DischargeActivity ParseAdcpDischarge(FieldVisitInfo visitInfo, AdcpDischargeDefinition adcpDischargeDefinition)
        {
            if (adcpDischargeDefinition == null)
                return null;

            var dischargeActivity = ParseDischargeActivity(visitInfo, adcpDischargeDefinition);

            var channelName = GetString(adcpDischargeDefinition.ChannelName) ?? ChannelMeasurementBaseConstants.DefaultChannelName;
            var distanceUnitId = GetString(adcpDischargeDefinition.DistanceUnitId);
            var areaUnitId = GetString(adcpDischargeDefinition.AreaUnitId);
            var velocityUnitId = GetString(adcpDischargeDefinition.VelocityUnitId);

            var sectionDischarge = GetNullableDouble(adcpDischargeDefinition.SectionDischarge)
                                   ?? dischargeActivity.Discharge.Value;

            var adcp = new AdcpDischargeSection(
                dischargeActivity.MeasurementPeriod,
                channelName,
                new Measurement(sectionDischarge, dischargeActivity.Discharge.UnitId),
                GetString(adcpDischargeDefinition.DeviceType),
                distanceUnitId,
                areaUnitId,
                velocityUnitId)
            {
                NumberOfTransects = GetNullableInteger(adcpDischargeDefinition.NumberOfTransects),
                MagneticVariation = GetNullableDouble(adcpDischargeDefinition.MagneticVariation),
                DischargeCoefficientVariation = GetNullableDouble(adcpDischargeDefinition.DischargeCoefficientVariation),
                PercentOfDischargeMeasured = GetNullableDouble(adcpDischargeDefinition.PercentageOfDischargeMeasured),
                WidthValue = GetNullableDouble(adcpDischargeDefinition.WidthValue),
                AreaValue = GetNullableDouble(adcpDischargeDefinition.AreaValue),
                VelocityAverageValue = GetNullableDouble(adcpDischargeDefinition.VelocityAverageValue),
                TransducerDepth = GetNullableDouble(adcpDischargeDefinition.TransducerDepth),
                TopEstimateExponent = GetNullableDouble(adcpDischargeDefinition.TopEstimateExponent),
                BottomEstimateExponent = GetNullableDouble(adcpDischargeDefinition.BottomEstimateExponent),
                FirmwareVersion = GetString(adcpDischargeDefinition.FirmwareVersion),
                SoftwareVersion = GetString(adcpDischargeDefinition.SoftwareVersion),
                MeasurementDevice = ParseMeasurementDevice(
                    adcpDischargeDefinition.MeasurementDeviceManufacturer,
                    adcpDischargeDefinition.MeasurementDeviceModel,
                    adcpDischargeDefinition.MeasurementDeviceSerialNumber)
            };

            var topEstimateMethod = GetString(adcpDischargeDefinition.TopEstimateMethod);

            if (!string.IsNullOrEmpty(topEstimateMethod))
                adcp.TopEstimateMethod = new TopEstimateMethodPickList(topEstimateMethod);

            var bottomEstimateMethod = GetString(adcpDischargeDefinition.BottomEstimateMethod);

            if (!string.IsNullOrEmpty(bottomEstimateMethod))
                adcp.BottomEstimateMethod = new BottomEstimateMethodPickList(bottomEstimateMethod);

            var navigationMethod = GetString(adcpDischargeDefinition.NavigationMethod);

            if (!string.IsNullOrEmpty(navigationMethod))
                adcp.NavigationMethod = new NavigationMethodPickList(navigationMethod);

            var depthReference = GetNullableEnum<DepthReferenceType>(adcpDischargeDefinition.DepthReference);

            if (depthReference.HasValue)
                adcp.DepthReference = depthReference.Value;

            var deploymentMethod = GetNullableEnum<AdcpDeploymentMethodType>(adcpDischargeDefinition.DeploymentMethod);

            if (deploymentMethod.HasValue)
                adcp.DeploymentMethod = deploymentMethod.Value;

            var meterSuspension = GetNullableEnum<AdcpMeterSuspensionType>(adcpDischargeDefinition.MeterSuspension);

            if (meterSuspension.HasValue)
                adcp.MeterSuspension = meterSuspension.Value;

            dischargeActivity.ChannelMeasurements.Add(adcp);

            return dischargeActivity;
        }

        private DischargeActivity ParsePanelSectionDischarge(FieldVisitInfo visitInfo, ManualGaugingDischargeDefinition panelDischargeDefinition)
        {
            if (panelDischargeDefinition == null)
                return null;

            var dischargeActivity = ParseDischargeActivity(visitInfo, panelDischargeDefinition);

            var channelName = GetString(panelDischargeDefinition.ChannelName) ?? ChannelMeasurementBaseConstants.DefaultChannelName;
            var distanceUnitId = GetString(panelDischargeDefinition.DistanceUnitId);
            var areaUnitId = GetString(panelDischargeDefinition.AreaUnitId);
            var velocityUnitId = GetString(panelDischargeDefinition.VelocityUnitId);

            var sectionDischarge = GetNullableDouble(panelDischargeDefinition.SectionDischarge)
                                   ?? dischargeActivity.Discharge.Value;

            var panel = new ManualGaugingDischargeSection(
                dischargeActivity.MeasurementPeriod,
                channelName,
                new Measurement(sectionDischarge, dischargeActivity.Discharge.UnitId),
                distanceUnitId,
                areaUnitId,
                velocityUnitId)
            {
                WidthValue = GetNullableDouble(panelDischargeDefinition.WidthValue),
                AreaValue = GetNullableDouble(panelDischargeDefinition.AreaValue),
                VelocityAverageValue = GetNullableDouble(panelDischargeDefinition.VelocityAverageValue),
                MeterCalibration = ParseMeterCalibration(panelDischargeDefinition),
            };

            var dischargeMethod = GetNullableEnum<DischargeMethodType>(panelDischargeDefinition.DischargeMethod);

            if (dischargeMethod.HasValue)
                panel.DischargeMethod = dischargeMethod.Value;

            var deploymentMethod = GetNullableEnum<DeploymentMethodType>(panelDischargeDefinition.DeploymentMethod);

            if (deploymentMethod.HasValue)
                panel.DeploymentMethod = deploymentMethod.Value;

            var meterSuspension = GetNullableEnum<MeterSuspensionType>(panelDischargeDefinition.MeterSuspension);

            if (meterSuspension.HasValue)
                panel.MeterSuspension = meterSuspension.Value;

            var startPoint = GetNullableEnum<StartPointType>(panelDischargeDefinition.StartPoint);

            if (startPoint.HasValue)
                panel.StartPoint = startPoint.Value;

            var velocityObservationMethod = GetNullableEnum<PointVelocityObservationType>(panelDischargeDefinition.VelocityObservationMethod);

            if (velocityObservationMethod.HasValue)
                panel.VelocityObservationMethod = velocityObservationMethod.Value;

            dischargeActivity.ChannelMeasurements.Add(panel);

            return dischargeActivity;
        }

        private MeterCalibration ParseMeterCalibration(ManualGaugingDischargeDefinition panelDischargeDefinition)
        {
            var meterProperties = new[]
                {
                    panelDischargeDefinition.MeterType,
                    panelDischargeDefinition.MeterCalibrationManufacturer,
                    panelDischargeDefinition.MeterCalibrationModel,
                    panelDischargeDefinition.MeterCalibrationSerialNumber,
                    panelDischargeDefinition.MeterCalibrationFirmwareVersion,
                    panelDischargeDefinition.MeterCalibrationSoftwareVersion,
                    panelDischargeDefinition.MeterCalibrationConfiguration,
                }
                .Where(p => p != null)
                .ToList();

            if (!panelDischargeDefinition.AllMeterCalibrationEquations.Any() && !meterProperties.Any())
                return null;

            var meterType = GetNullableEnum<MeterType>(panelDischargeDefinition.MeterType);

            var meterCalibration = new MeterCalibration
            {
                Manufacturer = GetString(panelDischargeDefinition.MeterCalibrationManufacturer),
                Model = GetString(panelDischargeDefinition.MeterCalibrationModel),
                SerialNumber = GetString(panelDischargeDefinition.MeterCalibrationSerialNumber),
                FirmwareVersion = GetString(panelDischargeDefinition.MeterCalibrationFirmwareVersion),
                SoftwareVersion = GetString(panelDischargeDefinition.MeterCalibrationSoftwareVersion),
                Configuration = GetString(panelDischargeDefinition.MeterCalibrationConfiguration),
            };

            if (meterType.HasValue)
                meterCalibration.MeterType = meterType.Value;

            foreach (var equationDefinition in panelDischargeDefinition.AllMeterCalibrationEquations)
            {
                var equation = new MeterCalibrationEquation
                {
                    Slope = GetDouble(equationDefinition),
                    Intercept = GetDouble(equationDefinition.Intercept),
                    InterceptUnitId = GetString(equationDefinition.InterceptUnitId),
                    RangeStart = GetNullableDouble(equationDefinition.RangeStart),
                    RangeEnd = GetNullableDouble(equationDefinition.RangeEnd),
                };

                meterCalibration.Equations.Add(equation);
            }

            return meterCalibration;
        }

        private DischargeActivity ParseDischargeActivity(FieldVisitInfo visitInfo, DischargeActivityDefinition dischargeDefinition)
        {
            var totalDischarge = GetDouble(dischargeDefinition);

            var dischargeUnitId = GetString(dischargeDefinition.DischargeUnitId);
            var dischargeInterval = ParseActivityTimeRange(visitInfo, dischargeDefinition);
            var discharge = new Measurement(totalDischarge, dischargeUnitId);

            var dischargeActivity = new DischargeActivity(dischargeInterval, discharge)
            {
                MeasurementId = GetString(dischargeDefinition.MeasurementId),
                Comments = GetString(dischargeDefinition.Comments),
                Party = GetString(dischargeDefinition.Party),
                AdjustmentAmount = GetNullableDouble(dischargeDefinition.AdjustmentAmount),
                AdjustmentType = GetNullableEnum<AdjustmentType>(dischargeDefinition.AdjustmentType),
                ReasonForAdjustment = GetNullableEnum<ReasonForAdjustmentType>(dischargeDefinition.ReasonForAdjustment),
                QualityAssuranceComments = GetString(dischargeDefinition.QualityAssuranceComments),
                QualitativeUncertainty = GetNullableEnum<QualitativeUncertaintyType>(dischargeDefinition.QualitativeUncertainty),
                QuantitativeUncertainty = GetNullableDouble(dischargeDefinition.QuantitativeUncertainty),
                MeanGageHeightDurationHours = GetNullableDouble(dischargeDefinition.MeanGageHeightDurationHours),
            };

            var showInDataCorrection = GetNullableBoolean(dischargeDefinition.ShowInDataCorrection);
            var showInRatingDevelopment = GetNullableBoolean(dischargeDefinition.ShowInRatingDevelopment);
            var preventAutomaticPublish = GetNullableBoolean(dischargeDefinition.PreventAutomaticPublishing);

            if (showInDataCorrection.HasValue)
                dischargeActivity.ShowInDataCorrection = showInDataCorrection.Value;

            if (showInRatingDevelopment.HasValue)
                dischargeActivity.ShowInRatingDevelopment = showInRatingDevelopment.Value;

            if (preventAutomaticPublish.HasValue)
                dischargeActivity.PreventAutomaticPublishing = preventAutomaticPublish.Value;

            var gradeCode = GetNullableInteger(dischargeDefinition.GradeCode);
            var gradeName = GetString(dischargeDefinition.GradeName);

            if (gradeCode.HasValue)
                dischargeActivity.MeasurementGrade = Grade.FromCode(gradeCode.Value);

            if (!string.IsNullOrEmpty(gradeName))
                dischargeActivity.MeasurementGrade = Grade.FromDisplayName(gradeName);

            var velocityUnitId = GetString(dischargeDefinition.VelocityUnitId);
            var meanIndexVelocity = GetNullableDouble(dischargeDefinition.MeanIndexVelocity);

            if (meanIndexVelocity.HasValue)
                dischargeActivity.MeanIndexVelocity = new Measurement(meanIndexVelocity.Value, velocityUnitId);

            var uncertaintyType = GetNullableEnum<UncertaintyType>(dischargeDefinition.UncertaintyType);

            if (uncertaintyType.HasValue)
                dischargeActivity.ActiveUncertaintyType = uncertaintyType.Value;

            var distanceUnitId = GetString(dischargeDefinition.DistanceUnitId);

            var manuallyCalculatedMeanGageHeight = GetNullableDouble(dischargeDefinition.ManuallyCalculatedMeanGageHeight);

            if (manuallyCalculatedMeanGageHeight.HasValue)
                dischargeActivity.ManuallyCalculatedMeanGageHeight = new Measurement(manuallyCalculatedMeanGageHeight.Value, distanceUnitId);

            var meanGageHeightDifferenceDuringVisit = GetNullableDouble(dischargeDefinition.MeanGageHeightDifferenceDuringVisit);

            if (meanGageHeightDifferenceDuringVisit.HasValue)
                dischargeActivity.MeanGageHeightDifferenceDuringVisit = new Measurement(meanGageHeightDifferenceDuringVisit.Value, distanceUnitId);

            foreach (var gageHeightMeasurement in dischargeDefinition.AllGageHeightMeasurements)
            {
                var gageHeightValue = GetDouble(gageHeightMeasurement);
                var include = GetNullableBoolean(gageHeightMeasurement.Include);
                var gageHeight = new GageHeightMeasurement(
                    new Measurement(gageHeightValue, distanceUnitId),
                    ParseActivityTime(visitInfo, gageHeightMeasurement, dischargeActivity.MeasurementStartTime),
                    include ?? true);

                dischargeActivity.GageHeightMeasurements.Add(gageHeight);
            }

            return dischargeActivity;
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

        private double GetDouble(ColumnDefinition column)
        {
            var value = GetNullableDouble(column);

            if (!value.HasValue)
                throw new InvalidOperationException($"Line {LineNumber} '{column.Name()}': '{GetString(column)}' is missing a required number.");

            return value.Value;
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
