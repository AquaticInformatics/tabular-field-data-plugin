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
using FieldDataPluginFramework.DataModel.LevelSurveys;
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

            var levelSurveys = Configuration
                .AllLevelSurveys
                .Select(l => ParseLevelSurvey(fieldVisitInfo, l))
                .Where(levelSurvey => levelSurvey != null)
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

            foreach (var levelSurvey in levelSurveys)
            {
                ResultsAppender.AddLevelSurvey(fieldVisitInfo, levelSurvey);
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
                Comments = MergeCommentText(visit),
                Party = GetString(visit.Party),
                CollectionAgency = GetString(visit.CollectionAgency),
                Weather = GetString(visit.Weather),
                CompletedVisitActivities = ParseCompletedVisitActivities(visit)
            };
        }

        private CompletedVisitActivities ParseCompletedVisitActivities(VisitDefinition definition)
        {
            return new CompletedVisitActivities
            {
                GroundWaterLevels = GetNullableBoolean(definition.CompletedGroundWaterLevels) ?? false,
                ConductedLevelSurvey = GetNullableBoolean(definition.CompletedLevelSurvey) ?? false,
                RecorderDataCollected = GetNullableBoolean(definition.CompletedRecorderData) ?? false,
                SafetyInspectionPerformed = GetNullableBoolean(definition.CompletedSafetyInspection) ?? false,
                OtherSample = GetNullableBoolean(definition.CompletedOtherSample) ?? false,
                BiologicalSample = GetNullableBoolean(definition.CompletedBiologicalSample) ?? false,
                SedimentSample = GetNullableBoolean(definition.CompletedSedimentSample) ?? false,
                WaterQualitySample = GetNullableBoolean(definition.CompletedWaterQualitySample) ?? false,
            };
        }

        private string MergeCommentText(CoreDefinition definition)
        {
            var lines = new List<string>();

            foreach (var column in definition.AllComments)
            {
                var value = GetString(column);

                if (string.IsNullOrWhiteSpace(value)) continue;

                if (lines.Any(l => l.IndexOf(value, StringComparison.InvariantCultureIgnoreCase) >= 0)) continue;

                lines.Add(value);
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

        private Reading ParseReading(FieldVisitInfo visitInfo, ReadingDefinition definition)
        {
            var readingValue = GetNullableDouble(definition.Value)
                               ?? GetNullableDouble(definition);

            if (!readingValue.HasValue)
                return null;

            var reading = new Reading(
                GetString(definition.ParameterId),
                new Measurement(readingValue.Value, GetString(definition.UnitId)))
            {
                DateTimeOffset = ParseActivityTime(visitInfo, definition),
                Comments = MergeCommentText(definition),
                ReferencePointName = GetString(definition.ReferencePointName),
                SubLocation = GetString(definition.SubLocation),
                SensorUniqueId = GetNullableGuid(definition.SensorUniqueId),
                Uncertainty = GetNullableDouble(definition.Uncertainty),
                MeasurementDevice = ParseMeasurementDevice(
                    definition.MeasurementDeviceManufacturer,
                    definition.MeasurementDeviceModel,
                    definition.MeasurementDeviceSerialNumber)
            };

            var readingType = GetNullableEnum<ReadingType>(definition.ReadingType);

            if (readingType.HasValue)
                reading.ReadingType = readingType.Value;

            var publish = GetNullableBoolean(definition.Publish);
            var useLocationDatumAsReference = GetNullableBoolean(definition.UseLocationDatumAsReference);

            var method = GetString(definition.Method);

            if (!string.IsNullOrWhiteSpace(method))
                reading.Method = method;

            var gradeCode = GetNullableInteger(definition.GradeCode);
            var gradeName = GetString(definition.GradeName);

            if (gradeCode.HasValue)
                reading.Grade = Grade.FromCode(gradeCode.Value);

            if (!string.IsNullOrEmpty(gradeName))
                reading.Grade = Grade.FromDisplayName(gradeName);

            if (publish.HasValue)
                reading.Publish = publish.Value;

            if (useLocationDatumAsReference.HasValue)
                reading.UseLocationDatumAsReference = useLocationDatumAsReference.Value;

            var readingQualifierSeparators = GetString(definition.ReadingQualifierSeparators);
            var readingQualifiers = GetString(definition.ReadingQualifiers);

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

            var measurementDetailsCut = GetNullableDouble(definition.MeasurementDetailsCut);
            var measurementDetailsHold = GetNullableDouble(definition.MeasurementDetailsHold);
            var measurementDetailsTapeCorrection = GetNullableDouble(definition.MeasurementDetailsTapeCorrection);
            var measurementDetailsWaterLevel = GetNullableDouble(definition.MeasurementDetailsWaterLevel);

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

        private Inspection ParseInspection(FieldVisitInfo visitInfo, InspectionDefinition definition)
        {
            var inspectionType = GetNullableEnum<InspectionType>(definition.InspectionType)
                                 ?? GetNullableEnum<InspectionType>(definition);

            if (!inspectionType.HasValue)
                return null;

            var inspection = new Inspection(inspectionType.Value)
            {
                DateTimeOffset = ParseActivityTime(visitInfo, definition),
                Comments = MergeCommentText(definition),
                SubLocation = GetString(definition.SubLocation),
                MeasurementDevice = ParseMeasurementDevice(
                    definition.MeasurementDeviceManufacturer,
                    definition.MeasurementDeviceModel,
                    definition.MeasurementDeviceSerialNumber)
            };

            return inspection;
        }

        private Calibration ParseCalibration(FieldVisitInfo visitInfo, CalibrationDefinition definition)
        {
            var calibrationValue = GetNullableDouble(definition.Value)
                                   ?? GetNullableDouble(definition);

            if (!calibrationValue.HasValue)
                return null;

            var calibration = new Calibration(
                GetString(definition.ParameterId),
                GetString(definition.UnitId),
                calibrationValue.Value)
            {
                DateTimeOffset = ParseActivityTime(visitInfo, definition),
                Comments = MergeCommentText(definition),
                Party = GetString(definition.Party),
                SubLocation = GetString(definition.SubLocation),
                SensorUniqueId = GetNullableGuid(definition.SensorUniqueId),
                Standard = GetNullableDouble(definition.Standard),
                MeasurementDevice = ParseMeasurementDevice(
                    definition.MeasurementDeviceManufacturer,
                    definition.MeasurementDeviceModel,
                    definition.MeasurementDeviceSerialNumber)
            };

            var calibrationType = GetNullableEnum<CalibrationType>(definition.CalibrationType);

            if (calibrationType.HasValue)
                calibration.CalibrationType = calibrationType.Value;

            var publish = GetNullableBoolean(definition.Publish);

            var method = GetString(definition.Method);

            if (!string.IsNullOrWhiteSpace(method))
                calibration.Method = method;

            if (publish.HasValue)
                calibration.Publish = publish.Value;

            DateTimeOffset? expirationDate = null;

            if (definition.StandardDetailsExpirationDate != null)
            {
                expirationDate = ParseDateTimeOffset(visitInfo.LocationInfo,
                    new List<TimestampDefinition>
                    {
                        definition.StandardDetailsExpirationDate
                    });
            }

            calibration.StandardDetails = new StandardDetails
            {
                LotNumber = GetString(definition.StandardDetailsLotNumber),
                StandardCode = GetString(definition.StandardDetailsStandardCode),
                ExpirationDate = expirationDate,
                Temperature = GetNullableDouble(definition.StandardDetailsTemperature),
            };

            return calibration;
        }

        private ControlCondition ParseControlCondition(FieldVisitInfo visitInfo, ControlConditionColumnDefinition definition)
        {
            if (definition == null)
                return null;

            DateTimeOffset? dateCleaned = null;

            if (definition.AllTimes?.Any() ?? false)
            {
                dateCleaned = ParseDateTimeOffset(visitInfo.LocationInfo, definition.AllTimes);
            }

            var conditionType = GetString(definition.ConditionType) ?? GetString(definition);
            var controlCode = GetString(definition.ControlCode);
            var controlCleanedType = GetNullableEnum<ControlCleanedType>(definition.ControlCleanedType);

            var controlCondition = new ControlCondition
            {
                Party = GetString(definition.Party),
                Comments = MergeCommentText(definition),
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

            var distanceToGage = GetNullableDouble(definition.DistanceToGage);

            var unitId = GetString(definition.UnitId);

            if (distanceToGage.HasValue)
            {
                controlCondition.DistanceToGage = new Measurement(distanceToGage.Value, unitId);
            }

            return controlCondition;
        }

        private DischargeActivity ParseAdcpDischarge(FieldVisitInfo visitInfo, AdcpDischargeDefinition definition)
        {
            var totalDischarge = GetNullableDouble(definition.TotalDischarge)
                                 ?? GetNullableDouble(definition);

            if (!totalDischarge.HasValue)
                return null;

            var dischargeActivity = ParseDischargeActivity(visitInfo, definition, totalDischarge.Value);

            var channelName = GetString(definition.ChannelName) ?? ChannelMeasurementBaseConstants.DefaultChannelName;
            var distanceUnitId = GetString(definition.DistanceUnitId);
            var areaUnitId = GetString(definition.AreaUnitId);
            var velocityUnitId = GetString(definition.VelocityUnitId);

            var sectionDischarge = GetNullableDouble(definition.SectionDischarge)
                                   ?? dischargeActivity.Discharge.Value;

            var adcp = new AdcpDischargeSection(
                dischargeActivity.MeasurementPeriod,
                channelName,
                new Measurement(sectionDischarge, dischargeActivity.Discharge.UnitId),
                GetString(definition.DeviceType),
                distanceUnitId,
                areaUnitId,
                velocityUnitId)
            {
                NumberOfTransects = GetNullableInteger(definition.NumberOfTransects),
                MagneticVariation = GetNullableDouble(definition.MagneticVariation),
                DischargeCoefficientVariation = GetNullableDouble(definition.DischargeCoefficientVariation),
                PercentOfDischargeMeasured = GetNullableDouble(definition.PercentageOfDischargeMeasured),
                WidthValue = GetNullableDouble(definition.WidthValue),
                AreaValue = GetNullableDouble(definition.AreaValue),
                VelocityAverageValue = GetNullableDouble(definition.VelocityAverageValue),
                TransducerDepth = GetNullableDouble(definition.TransducerDepth),
                TopEstimateExponent = GetNullableDouble(definition.TopEstimateExponent),
                BottomEstimateExponent = GetNullableDouble(definition.BottomEstimateExponent),
                FirmwareVersion = GetString(definition.FirmwareVersion),
                SoftwareVersion = GetString(definition.SoftwareVersion),
                MeasurementDevice = ParseMeasurementDevice(
                    definition.MeasurementDeviceManufacturer,
                    definition.MeasurementDeviceModel,
                    definition.MeasurementDeviceSerialNumber)
            };

            var topEstimateMethod = GetString(definition.TopEstimateMethod);

            if (!string.IsNullOrEmpty(topEstimateMethod))
                adcp.TopEstimateMethod = new TopEstimateMethodPickList(topEstimateMethod);

            var bottomEstimateMethod = GetString(definition.BottomEstimateMethod);

            if (!string.IsNullOrEmpty(bottomEstimateMethod))
                adcp.BottomEstimateMethod = new BottomEstimateMethodPickList(bottomEstimateMethod);

            var navigationMethod = GetString(definition.NavigationMethod);

            if (!string.IsNullOrEmpty(navigationMethod))
                adcp.NavigationMethod = new NavigationMethodPickList(navigationMethod);

            var depthReference = GetNullableEnum<DepthReferenceType>(definition.DepthReference);

            if (depthReference.HasValue)
                adcp.DepthReference = depthReference.Value;

            var deploymentMethod = GetNullableEnum<AdcpDeploymentMethodType>(definition.DeploymentMethod);

            if (deploymentMethod.HasValue)
                adcp.DeploymentMethod = deploymentMethod.Value;

            var meterSuspension = GetNullableEnum<AdcpMeterSuspensionType>(definition.MeterSuspension);

            if (meterSuspension.HasValue)
                adcp.MeterSuspension = meterSuspension.Value;

            dischargeActivity.ChannelMeasurements.Add(adcp);

            return dischargeActivity;
        }

        private DischargeActivity ParsePanelSectionDischarge(FieldVisitInfo visitInfo, ManualGaugingDischargeDefinition definition)
        {
            var totalDischarge = GetNullableDouble(definition.TotalDischarge)
                                 ?? GetNullableDouble(definition);

            if (!totalDischarge.HasValue)
                return null;

            var dischargeActivity = ParseDischargeActivity(visitInfo, definition, totalDischarge.Value);

            var channelName = GetString(definition.ChannelName) ?? ChannelMeasurementBaseConstants.DefaultChannelName;
            var distanceUnitId = GetString(definition.DistanceUnitId);
            var areaUnitId = GetString(definition.AreaUnitId);
            var velocityUnitId = GetString(definition.VelocityUnitId);

            var sectionDischarge = GetNullableDouble(definition.SectionDischarge)
                                   ?? dischargeActivity.Discharge.Value;

            var panel = new ManualGaugingDischargeSection(
                dischargeActivity.MeasurementPeriod,
                channelName,
                new Measurement(sectionDischarge, dischargeActivity.Discharge.UnitId),
                distanceUnitId,
                areaUnitId,
                velocityUnitId)
            {
                WidthValue = GetNullableDouble(definition.WidthValue),
                AreaValue = GetNullableDouble(definition.AreaValue),
                VelocityAverageValue = GetNullableDouble(definition.VelocityAverageValue),
                MeterCalibration = ParseMeterCalibration(definition),
            };

            var dischargeMethod = GetNullableEnum<DischargeMethodType>(definition.DischargeMethod);

            if (dischargeMethod.HasValue)
                panel.DischargeMethod = dischargeMethod.Value;

            var deploymentMethod = GetNullableEnum<DeploymentMethodType>(definition.DeploymentMethod);

            if (deploymentMethod.HasValue)
                panel.DeploymentMethod = deploymentMethod.Value;

            var meterSuspension = GetNullableEnum<MeterSuspensionType>(definition.MeterSuspension);

            if (meterSuspension.HasValue)
                panel.MeterSuspension = meterSuspension.Value;

            var startPoint = GetNullableEnum<StartPointType>(definition.StartPoint);

            if (startPoint.HasValue)
                panel.StartPoint = startPoint.Value;

            var velocityObservationMethod = GetNullableEnum<PointVelocityObservationType>(definition.VelocityObservationMethod);

            if (velocityObservationMethod.HasValue)
                panel.VelocityObservationMethod = velocityObservationMethod.Value;

            dischargeActivity.ChannelMeasurements.Add(panel);

            return dischargeActivity;
        }

        private MeterCalibration ParseMeterCalibration(ManualGaugingDischargeDefinition definition)
        {
            var meterProperties = new[]
                {
                    definition.MeterType,
                    definition.MeterCalibrationManufacturer,
                    definition.MeterCalibrationModel,
                    definition.MeterCalibrationSerialNumber,
                    definition.MeterCalibrationFirmwareVersion,
                    definition.MeterCalibrationSoftwareVersion,
                    definition.MeterCalibrationConfiguration,
                }
                .Where(p => p != null)
                .ToList();

            if (!definition.AllMeterCalibrationEquations.Any() && !meterProperties.Any())
                return null;

            var meterType = GetNullableEnum<MeterType>(definition.MeterType);

            var meterCalibration = new MeterCalibration
            {
                Manufacturer = GetString(definition.MeterCalibrationManufacturer),
                Model = GetString(definition.MeterCalibrationModel),
                SerialNumber = GetString(definition.MeterCalibrationSerialNumber),
                FirmwareVersion = GetString(definition.MeterCalibrationFirmwareVersion),
                SoftwareVersion = GetString(definition.MeterCalibrationSoftwareVersion),
                Configuration = GetString(definition.MeterCalibrationConfiguration),
            };

            if (meterType.HasValue)
                meterCalibration.MeterType = meterType.Value;

            foreach (var equationDefinition in definition.AllMeterCalibrationEquations)
            {
                var equation = ParseMeterCalibrationEquation(equationDefinition);

                if (equation == null)
                    continue;

                meterCalibration.Equations.Add(equation);
            }

            return meterCalibration;
        }

        private MeterCalibrationEquation ParseMeterCalibrationEquation(MeterCalibrationEquationDefinition definition)
        {
            var slope = GetNullableDouble(definition.Slope)
                        ?? GetNullableDouble(definition);

            if (!slope.HasValue)
                return null;

            return new MeterCalibrationEquation
            {
                Slope = slope.Value,
                Intercept = GetDouble(definition.Intercept),
                InterceptUnitId = GetString(definition.InterceptUnitId),
                RangeStart = GetNullableDouble(definition.RangeStart),
                RangeEnd = GetNullableDouble(definition.RangeEnd),
            };
        }

        private DischargeActivity ParseDischargeActivity(FieldVisitInfo visitInfo, DischargeActivityDefinition definition, double totalDischarge)
        {
            var dischargeUnitId = GetString(definition.DischargeUnitId);
            var dischargeInterval = ParseActivityTimeRange(visitInfo, definition);
            var discharge = new Measurement(totalDischarge, dischargeUnitId);

            var dischargeActivity = new DischargeActivity(dischargeInterval, discharge)
            {
                MeasurementId = GetString(definition.MeasurementId),
                Comments = MergeCommentText(definition),
                Party = GetString(definition.Party),
                AdjustmentAmount = GetNullableDouble(definition.AdjustmentAmount),
                AdjustmentType = GetNullableEnum<AdjustmentType>(definition.AdjustmentType),
                ReasonForAdjustment = GetNullableEnum<ReasonForAdjustmentType>(definition.ReasonForAdjustment),
                QualityAssuranceComments = GetString(definition.QualityAssuranceComments),
                QualitativeUncertainty = GetNullableEnum<QualitativeUncertaintyType>(definition.QualitativeUncertainty),
                QuantitativeUncertainty = GetNullableDouble(definition.QuantitativeUncertainty),
                MeanGageHeightDurationHours = GetNullableDouble(definition.MeanGageHeightDurationHours),
            };

            var showInDataCorrection = GetNullableBoolean(definition.ShowInDataCorrection);
            var showInRatingDevelopment = GetNullableBoolean(definition.ShowInRatingDevelopment);
            var preventAutomaticPublish = GetNullableBoolean(definition.PreventAutomaticPublishing);

            if (showInDataCorrection.HasValue)
                dischargeActivity.ShowInDataCorrection = showInDataCorrection.Value;

            if (showInRatingDevelopment.HasValue)
                dischargeActivity.ShowInRatingDevelopment = showInRatingDevelopment.Value;

            if (preventAutomaticPublish.HasValue)
                dischargeActivity.PreventAutomaticPublishing = preventAutomaticPublish.Value;

            var gradeCode = GetNullableInteger(definition.GradeCode);
            var gradeName = GetString(definition.GradeName);

            if (gradeCode.HasValue)
                dischargeActivity.MeasurementGrade = Grade.FromCode(gradeCode.Value);

            if (!string.IsNullOrEmpty(gradeName))
                dischargeActivity.MeasurementGrade = Grade.FromDisplayName(gradeName);

            var velocityUnitId = GetString(definition.VelocityUnitId);
            var meanIndexVelocity = GetNullableDouble(definition.MeanIndexVelocity);

            if (meanIndexVelocity.HasValue)
                dischargeActivity.MeanIndexVelocity = new Measurement(meanIndexVelocity.Value, velocityUnitId);

            var uncertaintyType = GetNullableEnum<UncertaintyType>(definition.UncertaintyType);

            if (uncertaintyType.HasValue)
                dischargeActivity.ActiveUncertaintyType = uncertaintyType.Value;

            var distanceUnitId = GetString(definition.DistanceUnitId);

            var manuallyCalculatedMeanGageHeight = GetNullableDouble(definition.ManuallyCalculatedMeanGageHeight);

            if (manuallyCalculatedMeanGageHeight.HasValue)
                dischargeActivity.ManuallyCalculatedMeanGageHeight = new Measurement(manuallyCalculatedMeanGageHeight.Value, distanceUnitId);

            var meanGageHeightDifferenceDuringVisit = GetNullableDouble(definition.MeanGageHeightDifferenceDuringVisit);

            if (meanGageHeightDifferenceDuringVisit.HasValue)
                dischargeActivity.MeanGageHeightDifferenceDuringVisit = new Measurement(meanGageHeightDifferenceDuringVisit.Value, distanceUnitId);

            foreach (var gageHeightMeasurementDefinition in definition.AllGageHeightMeasurements)
            {
                var gageHeightMeasurement = ParseGageHeightMeasurement(
                    visitInfo,
                    gageHeightMeasurementDefinition,
                    distanceUnitId,
                    dischargeActivity.MeasurementStartTime);

                if (gageHeightMeasurement == null)
                    continue;

                dischargeActivity.GageHeightMeasurements.Add(gageHeightMeasurement);
            }

            return dischargeActivity;
        }

        private GageHeightMeasurement ParseGageHeightMeasurement(
            FieldVisitInfo visitInfo,
            GageHeightMeasurementDefinition definition,
            string distanceUnitId,
            DateTimeOffset dischargeTime)
        {
            var gageHeightValue = GetNullableDouble(definition.Value)
                                  ?? GetNullableDouble(definition);

            if (!gageHeightValue.HasValue)
                return null;

            var include = GetNullableBoolean(definition.Include);

            return new GageHeightMeasurement(
                new Measurement(gageHeightValue.Value, distanceUnitId),
                ParseActivityTime(visitInfo, definition, dischargeTime),
                include ?? true);
        }

        private LevelSurvey ParseLevelSurvey(FieldVisitInfo visitInfo, LevelSurveyDefinition definition)
        {
            var originReferencePointName = GetString(definition.OriginReferencePointName)
                                           ?? GetString(definition);

            if (string.IsNullOrEmpty(originReferencePointName))
                return null;

            var levelSurvey = new LevelSurvey(originReferencePointName)
            {
                Comments = MergeCommentText(definition),
                Party = GetString(definition.Party),
            };

            var method = GetString(definition.Method);

            if (!string.IsNullOrEmpty(method))
                levelSurvey.Method = method;

            levelSurvey.LevelSurveyMeasurements = definition
                .AllLevelSurveyMeasurements
                .Select(m => ParseLevelSurveyMeasurement(visitInfo, m))
                .Where(m => m != null)
                .ToList();

            return levelSurvey;
        }

        private LevelSurveyMeasurement ParseLevelSurveyMeasurement(FieldVisitInfo visitInfo, LevelSurveyMeasurementDefinition definition)
        {
            var measuredElevation = GetNullableDouble(definition.MeasuredElevation)
                                    ?? GetNullableDouble(definition);

            if (!measuredElevation.HasValue)
                return null;

            return new LevelSurveyMeasurement(
                GetString(definition.ReferencePointName),
                ParseActivityTime(visitInfo, definition),
                measuredElevation.Value)
            {
                Comments = MergeCommentText(definition),
            };
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
