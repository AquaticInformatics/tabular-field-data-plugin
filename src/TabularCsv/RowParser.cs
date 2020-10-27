using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
using Humanizer;

namespace TabularCsv
{
    public class RowParser
    {
        public Configuration Configuration { get; set; }
        public long LineNumber { get; set; }
        public ILog Log { get; set; }
        public DelayedAppender ResultsAppender { get; set; }
        public LocationInfo LocationInfo { get; set; }
        public int RemainingHeaderLines { get; set; }

        public int PrefaceLineCount => PrefaceLines.Count;

        private string[] Fields { get; set; }
        private List<string> PrefaceLines { get; } = new List<string>();
        private string MultilinePreface { get; set; }
        private Dictionary<Regex, string> PrefaceRegexMatches { get; } = new Dictionary<Regex, string>();
        private Dictionary<string, int> ColumnHeaderMap { get; set; } = new Dictionary<string, int>();
        private Exception LastHeaderException { get; set; }

        public void Parse(string[] fields)
        {
            Fields = fields;

            ParseRow();
        }

        public void AddPrefaceLines(IEnumerable<string> prefaceLines)
        {
            PrefaceLines.AddRange(prefaceLines);

            MultilinePreface = string.Join(Environment.NewLine, PrefaceLines);

            BuildPrefaceRegexMatches();
        }

        private void BuildPrefaceRegexMatches()
        {
            var regexColumns = Configuration
                .GetColumnDefinitions()
                .Where(column => column.HasPrefaceRegex)
                .ToList();

            foreach (var regexColumn in regexColumns)
            {
                if (regexColumn.HasMultilineRegex)
                {
                    AddPrefaceRegexMatch(regexColumn.PrefaceRegex, regexColumn.PrefaceRegex.Match(MultilinePreface));
                    continue;
                }

                foreach (var match in PrefaceLines.Select(prefaceLine => regexColumn.PrefaceRegex.Match(prefaceLine)).Where(match => match.Success))
                {
                    AddPrefaceRegexMatch(regexColumn.PrefaceRegex, match);
                }
            }

            var unmatchedRegexColumns = regexColumns
                .Where(r => !PrefaceRegexMatches.ContainsKey(r.PrefaceRegex))
                .ToList();

            if (Configuration.StrictMode && unmatchedRegexColumns.Any())
                throw new ArgumentException($"{"preface regex column".ToQuantity(unmatchedRegexColumns.Count)} did not match anything from the {"preface line".ToQuantity(PrefaceLines.Count)}: {string.Join(", ", unmatchedRegexColumns.Select(c => c.Name()))}");
        }

        private void AddPrefaceRegexMatch(Regex regex, Match match)
        {
            if (!match.Success)
            {
                return;
            }

            var value = match
                .Groups
                .Cast<Group>()
                .First(g => ColumnDefinition.RegexCaptureGroupName.Equals(g.Name, StringComparison.InvariantCultureIgnoreCase))
                .Value
                .Trim();

            PrefaceRegexMatches[regex] = value;
        }

        public bool IsPrefaceValid()
        {
            if (!string.IsNullOrEmpty(Configuration.PrefaceMustContain) &&
                MultilinePreface.IndexOf(Configuration.PrefaceMustContain, StringComparison.CurrentCultureIgnoreCase) < 0)
                return false;

            if (Configuration.PrefaceMustMatchRegex != null &&
                !Configuration.PrefaceMustMatchRegex.IsMatch(MultilinePreface))
                return false;

            return true;
        }

        public bool IsHeaderFullyParsed(string[] headerFields)
        {
            if (!ColumnHeaderMap.Any())
            {
                var validator = new ConfigurationValidator
                {
                    Configuration = Configuration
                };

                try
                {
                    ColumnHeaderMap = validator.BuildColumnHeaderMap(headerFields);
                }
                catch (Exception exception)
                {
                    LastHeaderException = exception;
                }
            }

            --RemainingHeaderLines;

            if (RemainingHeaderLines > 0)
                return false;

            if (!ColumnHeaderMap.Any() && LastHeaderException != null)
                throw LastHeaderException;

            return true;
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

            var controlConditions = new[]
                {
                    ParseControlCondition(fieldVisitInfo, Configuration.ControlCondition)
                }
                .Where(controlCondition => controlCondition != null)
                .ToList();

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

            MergeParsedActivities(
                locationInfo,
                fieldVisitInfo,
                readings,
                inspections,
                calibrations,
                controlConditions,
                discharges,
                levelSurveys);
        }

        private void MergeParsedActivities(
            LocationInfo locationInfo,
            FieldVisitInfo fieldVisitInfo,
            List<Reading> readings,
            List<Inspection> inspections,
            List<Calibration> calibrations,
            List<ControlCondition> controlConditions,
            List<DischargeActivity> discharges,
            List<LevelSurvey> levelSurveys)
        {
            // Add all the activities to the visit
            foreach (var reading in readings)
            {
                fieldVisitInfo.Readings.Add(reading);
            }

            foreach (var inspection in inspections)
            {
                fieldVisitInfo.Inspections.Add(inspection);
            }

            foreach (var calibration in calibrations)
            {
                fieldVisitInfo.Calibrations.Add(calibration);
            }

            foreach (var controlCondition in controlConditions)
            {
                fieldVisitInfo.ControlConditions.Add(controlCondition);
            }

            foreach (var discharge in discharges)
            {
                fieldVisitInfo.DischargeActivities.Add(discharge);
            }

            foreach (var levelSurvey in levelSurveys)
            {
                fieldVisitInfo.LevelSurveys.Add(levelSurvey);
            }

            // Now ensure the visit actually contains all of its activities
            ResultsAppender.AdjustVisitPeriodToContainAllActivities(fieldVisitInfo);

            ThrowIfNoVisitTimes(fieldVisitInfo, locationInfo);

            var mergedVisit = ResultsAppender.AddFieldVisit(locationInfo, fieldVisitInfo.FieldVisitDetails);

            // Now add all the activities into the merged visit
            foreach (var reading in readings)
            {
                ResultsAppender.AddReading(mergedVisit, reading);
            }

            foreach (var inspection in inspections)
            {
                ResultsAppender.AddInspection(mergedVisit, inspection);
            }

            foreach (var calibration in calibrations)
            {
                ResultsAppender.AddCalibration(mergedVisit, calibration);
            }

            foreach (var controlCondition in controlConditions)
            {
                ResultsAppender.AddControlCondition(mergedVisit, controlCondition);
            }

            foreach (var discharge in discharges)
            {
                ResultsAppender.AddDischargeActivity(mergedVisit, discharge);
            }

            foreach (var levelSurvey in levelSurveys)
            {
                ResultsAppender.AddLevelSurvey(mergedVisit, levelSurvey);
            }
        }

        private void ThrowIfNoVisitTimes(FieldVisitInfo fieldVisitInfo, LocationInfo locationInfo)
        {
            if (fieldVisitInfo.StartDate != DateTimeOffset.MinValue && fieldVisitInfo.EndDate != DateTimeOffset.MaxValue)
                return;

            var allTimeColumns = Configuration.AllTimes
                .Concat(Configuration.AllStartTimes)
                .Concat(Configuration.AllEndTimes)
                .Concat(Configuration.Visit.AllTimes)
                .Concat(Configuration.Visit.AllStartTimes)
                .Concat(Configuration.Visit.AllEndTimes)
                .ToList();

            if (!allTimeColumns.Any())
                throw new Exception($"Line {LineNumber}: '{locationInfo.LocationIdentifier}': No timestamp columns are configured and none of the visit activities have a valid timestamp.");

            throw new Exception($"Line {LineNumber}: '{locationInfo.LocationIdentifier}': No timestamp could be calculated from these columns: {string.Join(", ", allTimeColumns.Select(c => c.Name()))}");
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
                                       Configuration.AllEndTimes)
                                   ?? new DateTimeInterval(DateTimeOffset.MinValue, DateTimeOffset.MaxValue); // This wide interval will be shrunk down later

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
            var lines = new[] {definition.Comment}
                .Where(d => d != null)
                .Select(GetString)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            foreach (var mergeWithComment in definition.AllMergeWithComments)
            {
                var value = GetString(mergeWithComment);

                if (string.IsNullOrWhiteSpace(value)) continue;

                if (lines.Any(l => l.IndexOf(value, StringComparison.CurrentCultureIgnoreCase) >= 0)) continue;

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

        private const IFormatProvider CurrentThreadCulture = null;

        private DateTimeOffset? ParseNullableDateTimeOffset(LocationInfo locationInfo, List<TimestampDefinition> timestampColumns)
        {
            if (!timestampColumns.Any())
                return null;

            var timestamp = new DateTimeOffset(new DateTime(1900, 1, 1), LocationInfo?.UtcOffset ?? locationInfo.UtcOffset);

            foreach (var timestampColumn in timestampColumns)
            {
                var timeText = GetString(timestampColumn);

                if (string.IsNullOrWhiteSpace(timeText))
                    continue;

                const DateTimeStyles styles = DateTimeStyles.AllowWhiteSpaces;

                var formats = timestampColumn.Formats;
                DateTimeOffset value;

                if (formats == null || !formats.Any())
                {
                    if (!DateTimeOffset.TryParse(timeText, CurrentThreadCulture, styles, out value))
                        throw new Exception($"Line {LineNumber}: '{timeText}' can't be parsed as a timestamp using the default format.");
                }
                else
                {
                    if (!DateTimeOffset.TryParseExact(timeText, timestampColumn.Formats, CurrentThreadCulture, styles, out value))
                        throw new Exception($"Line {LineNumber}: '{timeText}' can't be parsed as a timestamp using the '{string.Join(", ", formats)}' {"format".ToQuantity(formats.Length, ShowQuantityAs.None)}.");
                }

                if (!timestampColumn.Type.HasValue)
                    throw new Exception($"{timestampColumn.Name()} has no configured {nameof(timestampColumn.Type)}: You must specify one of {string.Join(", ", Enum.GetNames(typeof(TimestampType)))}");

                if (!TimestampParsers.TryGetValue(timestampColumn.Type.Value, out var timeParser))
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
                value.Date,
                existing.Offset)
                .Add(existing.TimeOfDay);
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
            var time = ParseNullableDateTimeOffset(visitInfo.LocationInfo, activity.AllTimes)
                       ?? fallbackTime
                       ?? visitInfo.StartDate;

            if (time != DateTimeOffset.MinValue)
                return time;

            var allTimeColumns = activity.AllTimes;

            if (!allTimeColumns.Any())
                throw new ArgumentException($"Line {LineNumber}: '{visitInfo.LocationInfo.LocationIdentifier}': Can't infer activity time because no timestamp columns are configured.");

            throw new ArgumentException($"Line {LineNumber}: '{visitInfo.LocationInfo.LocationIdentifier}': No timestamp could be calculated from these columns: {string.Join(", ", allTimeColumns.Select(c => c.Name()))}");
        }

        private DateTimeInterval ParseActivityTimeRange(FieldVisitInfo visitInfo, TimeRangeActivityDefinition timeRangeActivity)
        {
            var interval = ParseInterval(
                       visitInfo.LocationInfo,
                       timeRangeActivity.AllTimes,
                       timeRangeActivity.AllStartTimes,
                       timeRangeActivity.AllEndTimes)
                   ?? visitInfo.FieldVisitDetails.FieldVisitPeriod;

            if (interval.Start != DateTimeOffset.MinValue || interval.End != DateTimeOffset.MaxValue)
                return interval;

            var allTimeColumns = timeRangeActivity.AllTimes
                .Concat(timeRangeActivity.AllStartTimes)
                .Concat(timeRangeActivity.AllEndTimes)
                .ToList();

            if (!allTimeColumns.Any())
                throw new ArgumentException($"Line {LineNumber}: '{visitInfo.LocationInfo.LocationIdentifier}': Can't infer activity time range because no timestamp columns are configured.");

            throw new ArgumentException($"Line {LineNumber}: '{visitInfo.LocationInfo.LocationIdentifier}': No time interval could be calculated from these columns: {string.Join(", ", allTimeColumns.Select(c => c.Name()))}");
        }

        private Reading ParseReading(FieldVisitInfo visitInfo, ReadingDefinition definition)
        {
            var allowEmptyValues = GetNullableBoolean(definition.AllowEmptyValues) ?? false;
            var readingValue = GetNullableDouble(definition.Value);
            var parameterId = GetString(definition.ParameterId);
            var readingUnitId = GetString(definition.UnitId);

            if (!allowEmptyValues && !readingValue.HasValue)
                return null;

            if (allowEmptyValues && string.IsNullOrEmpty(parameterId))
                return null;

            var reading = readingValue.HasValue
                ? new Reading(
                    parameterId,
                    new Measurement(readingValue.Value, readingUnitId))
                : new Reading(
                    parameterId,
                    readingUnitId,
                    null);

            reading.DateTimeOffset = ParseActivityTime(visitInfo, definition);
            reading.Comments = MergeCommentText(definition);
            reading.ReferencePointName = GetString(definition.ReferencePointName);
            reading.SubLocation = GetString(definition.SubLocation);
            reading.SensorUniqueId = GetNullableGuid(definition.SensorUniqueId);
            reading.Uncertainty = GetNullableDouble(definition.Uncertainty);
            reading.MeasurementDevice = ParseMeasurementDevice(
                definition.MeasurementDeviceManufacturer,
                definition.MeasurementDeviceModel,
                definition.MeasurementDeviceSerialNumber);

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
                var qualifiers = new[] { readingQualifiers };

                if (!string.IsNullOrWhiteSpace(readingQualifierSeparators))
                {
                    qualifiers = readingQualifiers
                        .Split(readingQualifierSeparators.ToArray())
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s))
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
            var inspectionType = GetNullableEnum<InspectionType>(definition.InspectionType);

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
            var calibrationValue = GetNullableDouble(definition.Value);

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
                expirationDate = ParseNullableDateTimeOffset(visitInfo.LocationInfo,
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

        private ControlCondition ParseControlCondition(FieldVisitInfo visitInfo, ControlConditionDefinition definition)
        {
            if (definition == null)
                return null;

            DateTimeOffset? dateCleaned = null;

            if (definition.AllTimes?.Any() ?? false)
            {
                dateCleaned = ParseNullableDateTimeOffset(visitInfo.LocationInfo, definition.AllTimes);
            }

            var conditionType = GetString(definition.ConditionType);
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
            var totalDischarge = GetNullableDouble(definition.TotalDischarge);

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
            var totalDischarge = GetNullableDouble(definition.TotalDischarge);

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
                NumberOfVerticals = GetNullableInteger(definition.NumberOfVerticals),
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
            var slope = GetNullableDouble(definition.Slope);

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
                MeasurementTime = ParseNullableDateTimeOffset(visitInfo.LocationInfo, definition.AllTimes),
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
            var gageHeightValue = GetNullableDouble(definition.Value);

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
            var originReferencePointName = GetString(definition.OriginReferencePointName);

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
            var measuredElevation = GetNullableDouble(definition.MeasuredElevation);

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

            if (int.TryParse(valueText, NumberStyles.Any, CurrentThreadCulture, out var value))
                return value;

            throw new ArgumentException($"Line {LineNumber} '{column.Name()}': '{valueText}' is an invalid integer.");
        }

        private double? GetNullableDouble(ColumnDefinition column)
        {
            var valueText = GetString(column);

            if (string.IsNullOrWhiteSpace(valueText))
                return null;

            if (double.TryParse(valueText, NumberStyles.Any, CurrentThreadCulture, out var value))
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

            var value = GetColumnValue(column);

            if (value == null)
                return null;

            if (column.HasAlias)
            {
                if (Configuration.Aliases.TryGetValue(column.Alias, out var aliasedValues)
                    && aliasedValues.TryGetValue(value, out var aliasedValue))
                {
                    return aliasedValue;
                }
            }

            return !string.IsNullOrWhiteSpace(value)
                ? value
                : null;
        }

        private string GetColumnValue(ColumnDefinition column)
        {
            if (column.HasPrefaceRegex)
                return PrefaceRegexMatches.TryGetValue(column.PrefaceRegex, out var value)
                    ? value
                    : null;

            if (column.HasFixedValue)
                return column.FixedValue;

            var fieldIndex = column.RequiresColumnHeader()
                ? ColumnHeaderMap[column.ColumnHeader]
                : column.ColumnIndex ?? 0;

            if (fieldIndex <= 0)
                throw new ArgumentException($"Line {LineNumber} '{column.Name()}' has an invalid index={fieldIndex}.");

            return fieldIndex <= Fields.Length
                ? Fields[fieldIndex - 1]
                : null;
        }
    }
}
