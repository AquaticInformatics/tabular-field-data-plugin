using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace TabularCsv
{
    public class Configuration
    {
        public string Name { get; set; }
        public int Priority { get; set; }
        public string Separator { get; set; }
        public int HeaderRowCount { get; set; }
        public string HeadersEndWith { get; set; }
        public string HeadersEndBefore { get; set; }
        public bool FirstDataRowIsColumnHeader { get; set; }
        public string CommentLinePrefix { get; set; }

        public PropertyDefinition Location { get; set; }
        public VisitDefinition Visit { get; set; }
        public ControlConditionColumnDefinition ControlCondition { get; set; }

        public List<ReadingDefinition> Readings { get; set; } = new List<ReadingDefinition>();
        public List<InspectionDefinition> Inspections { get; set; } = new List<InspectionDefinition>();
        public List<CalibrationDefinition> Calibrations { get; set; } = new List<CalibrationDefinition>();
        public List<AdcpDischargeDefinition> AdcpDischarges { get; set; } = new List<AdcpDischargeDefinition>();
        public List<ManualGaugingDischargeDefinition> PanelSectionDischarges { get; set; } = new List<ManualGaugingDischargeDefinition>();

        public bool IsHeaderSectionExpected => HeaderRowCount > 0
                                               || !string.IsNullOrEmpty(HeadersEndWith)
                                               || !string.IsNullOrEmpty(HeadersEndBefore);

        public bool IsHeaderRowRequired => FirstDataRowIsColumnHeader
                                           || GetColumnDefinitions().Any(c => c.RequiresColumnHeader());

        private List<ColumnDefinition> ColumnDefinitions { get; set; }

        public List<ColumnDefinition> GetColumnDefinitions()
        {
            if (ColumnDefinitions == null)
            {
                ColumnDefinitions = ColumnDecorator.GetNamedColumns(this);
            }

            return ColumnDefinitions;
        }
    }

    public static class ColumnDecorator
    {
        public static List<ColumnDefinition> GetNamedColumns(object item, string baseName = "")
        {
            var columnDefinitions = new List<ColumnDefinition>();

            if (item == null)
                return columnDefinitions;

            if (item is ColumnDefinition itemDefinition)
            {
                columnDefinitions.Add(itemDefinition);
                itemDefinition.SetNamePrefix(baseName);
            }

            var type = item.GetType();
            var baseColumnType = typeof(ColumnDefinition);
            var baseListType = typeof(List<>);
            var propertyDefinitionType = typeof(PropertyDefinition);

            void AddNamedColumn(ColumnDefinition column, string propertyName)
            {
                var columnPropertyType = column.GetType();

                if (propertyDefinitionType.IsAssignableFrom(columnPropertyType))
                {
                    columnDefinitions.Add(column);
                    column.SetNamePrefix($"{baseName}.{propertyName}");
                }
                else
                {
                    columnDefinitions.AddRange(GetNamedColumns(column, $"{baseName}.{propertyName}"));
                }
            }

            var candidateProperties = type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.CanRead && p.CanWrite)
                .ToList();

            var columnProperties = candidateProperties
                .Where(p => baseColumnType.IsAssignableFrom(p.PropertyType))
                .ToList();

            foreach (var columnProperty in columnProperties)
            {
                var getMethod = columnProperty.GetGetMethod(false);

                if (getMethod == null)
                    continue;

                var column = getMethod.Invoke(item, null) as ColumnDefinition;

                if (column == null)
                    continue;

                AddNamedColumn(column, columnProperty.Name);
            }

            var columnCollectionProperties = candidateProperties
                .Where(p => p.PropertyType.IsGenericType && baseListType == p.PropertyType.GetGenericTypeDefinition())
                .ToList();

            foreach (var columnCollectionProperty in columnCollectionProperties)
            {
                var getMethod = columnCollectionProperty.GetGetMethod(false);

                if (getMethod == null)
                    continue;

                var list = getMethod.Invoke(item, null) as IEnumerable;

                if (list == null)
                    continue;

                var index = 1;
                foreach (var listItem in list)
                {
                    if (listItem is ColumnDefinition column)
                    {
                        AddNamedColumn(column, $"{columnCollectionProperty.Name}[{index}]");
                    }

                    ++index;
                }
            }

            return columnDefinitions;
        }
    }

    public class PropertyDefinition : ColumnDefinition
    {
    }

    public abstract class ColumnDefinition
    {
        public const string RegexCaptureGroupName = "value";

        public int? ColumnIndex { get; set; }
        public string ColumnHeader { get; set; }
        public string FixedValue { get; set; }
        public string HeaderRegex { get; set; }
        private string NamePrefix { get; set; }

        public void SetNamePrefix(string namePrefix)
        {
            if (namePrefix?.StartsWith(".") ?? false)
            {
                namePrefix = namePrefix.Substring(1);
            }

            NamePrefix = namePrefix;
        }

        public bool RequiresColumnHeader()
        {
            return !HasFixedValue
                   && !HasHeaderRegex
                   && HasNamedColumn;
        }

        public bool HasFixedValue => !string.IsNullOrEmpty(FixedValue);
        public bool HasNamedColumn => !string.IsNullOrWhiteSpace(ColumnHeader);
        public bool HasIndexedColumn => ColumnIndex.HasValue;
        public bool HasHeaderRegex => !string.IsNullOrEmpty(HeaderRegex);

        public bool IsInvalid(out string validationMessage)
        {
            validationMessage = null;

            if (HasHeaderRegex)
            {
                var regex = new Regex(HeaderRegex);

                if (!regex.GetGroupNames().Contains(RegexCaptureGroupName))
                {
                    validationMessage = $"A named capture group is missing. Use something like: (?<{RegexCaptureGroupName}>PATTERN)";

                    return true;
                }
            }

            var setProperties = new[]
                {
                    HasFixedValue ? nameof(FixedValue) : null,
                    HasHeaderRegex ? nameof(HeaderRegex) : null,
                    HasNamedColumn ? nameof(ColumnHeader) : null,
                    HasIndexedColumn ? nameof(ColumnIndex) : null,
                }
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            if (setProperties.Count != 1)
            {
                var allProperties = new[]
                {
                    nameof(ColumnHeader),
                    nameof(ColumnIndex),
                    nameof(FixedValue),
                    nameof(HeaderRegex),
                };

                var setPropertyContext = setProperties.Any()
                    ? $": {string.Join(", ", setProperties)}"
                    : string.Empty;

                validationMessage = $"Only one of the {string.Join(", ", allProperties)} properties can be set. You have set {setProperties.Count} properties{setPropertyContext}.";

                return true;
            }

            return false;
        }

        public void AllowUnusedDefaultProperty()
        {
            if (IsInvalid(out _))
            {
                FixedValue = "?Unused?";
                ColumnHeader = null;
                ColumnIndex = null;
                HeaderRegex = null;
            }
        }

        public string Name()
        {
            var suffix = RequiresColumnHeader()
                ? $"ColumnHeader='{ColumnHeader}'"
                : HasFixedValue
                    ? $"FixedValue='{FixedValue}'"
                    : HasHeaderRegex
                        ? $"HeaderRegex='{HeaderRegex}'"
                        : HasIndexedColumn
                            ? $"ColumnIndex[{ColumnIndex}]"
                            : "NoContextSpecified";

            return $"{NamePrefix}.{suffix}";
        }
    }

    public class MergingTextDefinition : ColumnDefinition
    {
        public string Prefix { get; set; }
    }

    public class TimestampDefinition : ColumnDefinition
    {
        public string Format { get; set; }
        public TimestampType Type { get; set; }
        public PropertyDefinition UtcOffset { get; set; }
    }

    public abstract class ActivityColumnDefinition : ColumnDefinition
    {
        public List<TimestampDefinition> Timestamps { get; set; } = new List<TimestampDefinition>();
    }

    public abstract class TimeRangeActivityColumnDefinition : ActivityColumnDefinition
    {
        public List<TimestampDefinition> StartTimestamps { get; set; } = new List<TimestampDefinition>();
        public List<TimestampDefinition> EndTimestamps { get; set; } = new List<TimestampDefinition>();
    }

    public class VisitDefinition : TimeRangeActivityColumnDefinition
    {
        // No default property for the main visit
        public PropertyDefinition Weather { get; set; }
        public PropertyDefinition CollectionAgency { get; set; }
        public PropertyDefinition CompletedGroundWaterLevels { get; set; }
        public PropertyDefinition CompletedLevelSurvey { get; set; }
        public PropertyDefinition CompletedRecorderData { get; set; }
        public PropertyDefinition CompletedSafetyInspection { get; set; }
        public PropertyDefinition CompletedOtherSample { get; set; }
        public PropertyDefinition CompletedBiologicalSample { get; set; }
        public PropertyDefinition CompletedSedimentSample { get; set; }
        public PropertyDefinition CompletedWaterQualitySample { get; set; }
        public List<MergingTextDefinition> Comments { get; set; } = new List<MergingTextDefinition>();
        public List<MergingTextDefinition> Party { get; set; } = new List<MergingTextDefinition>();
    }

    public class ReadingDefinition : ActivityColumnDefinition
    {
        // Default property is Reading.Value
        public PropertyDefinition ParameterId { get; set; }
        public PropertyDefinition UnitId { get; set; }
        public string CommentPrefix { get; set; }
        public PropertyDefinition ReadingType { get; set; }
        public PropertyDefinition Comments { get; set; }
        public PropertyDefinition GradeCode { get; set; }
        public PropertyDefinition GradeName { get; set; }
        public PropertyDefinition Method { get; set; }
        public PropertyDefinition Publish { get; set; }
        public PropertyDefinition ReferencePointName { get; set; }
        public PropertyDefinition SensorUniqueId { get; set; }
        public PropertyDefinition SubLocation { get; set; }
        public PropertyDefinition UseLocationDatumAsReference { get; set; }
        public PropertyDefinition Uncertainty { get; set; }
        public PropertyDefinition MeasurementDeviceManufacturer { get; set; }
        public PropertyDefinition MeasurementDeviceModel { get; set; }
        public PropertyDefinition MeasurementDeviceSerialNumber { get; set; }
        public PropertyDefinition ReadingQualifiers { get; set; }
        public PropertyDefinition ReadingQualifierSeparators { get; set; }
        public PropertyDefinition MeasurementDetailsCut { get; set; }
        public PropertyDefinition MeasurementDetailsHold { get; set; }
        public PropertyDefinition MeasurementDetailsTapeCorrection { get; set; }
        public PropertyDefinition MeasurementDetailsWaterLevel { get; set; }
    }

    public class InspectionDefinition : ActivityColumnDefinition
    {
        // Default property is Inspection.InspectionType (enum)
        public PropertyDefinition Comments { get; set; }
        public PropertyDefinition SubLocation { get; set; }
        public PropertyDefinition MeasurementDeviceManufacturer { get; set; }
        public PropertyDefinition MeasurementDeviceModel { get; set; }
        public PropertyDefinition MeasurementDeviceSerialNumber { get; set; }
    }

    public class CalibrationDefinition : ActivityColumnDefinition
    {
        // Default property is Calibration.Value
        public PropertyDefinition ParameterId { get; set; }
        public PropertyDefinition UnitId { get; set; }
        public PropertyDefinition Comments { get; set; }
        public PropertyDefinition Party { get; set; }
        public PropertyDefinition CalibrationType { get; set; }
        public PropertyDefinition Method { get; set; }
        public PropertyDefinition Publish { get; set; }
        public PropertyDefinition SensorUniqueId { get; set; }
        public PropertyDefinition SubLocation { get; set; }
        public PropertyDefinition Standard { get; set; }
        public PropertyDefinition MeasurementDeviceManufacturer { get; set; }
        public PropertyDefinition MeasurementDeviceModel { get; set; }
        public PropertyDefinition MeasurementDeviceSerialNumber { get; set; }
        public PropertyDefinition StandardDetailsLotNumber { get; set; }
        public PropertyDefinition StandardDetailsStandardCode { get; set; }
        public TimestampDefinition StandardDetailsExpirationDate { get; set; }
        public PropertyDefinition StandardDetailsTemperature { get; set; }
    }

    public class ControlConditionColumnDefinition : ActivityColumnDefinition
    {
        // Default property is ControlCondition.ConditionType (picklist)
        public PropertyDefinition UnitId { get; set; }
        public PropertyDefinition Comments { get; set; }
        public PropertyDefinition Party { get; set; }
        public PropertyDefinition ControlCleanedType { get; set; }
        public PropertyDefinition ControlCode { get; set; }
        public PropertyDefinition DistanceToGage { get; set; }
    }

    public abstract class DischargeActivityColumnDefinition : TimeRangeActivityColumnDefinition
    {
        // Default property is DischargeActivity.TotalDischarge.Value
        public PropertyDefinition ChannelName { get; set; }
        public PropertyDefinition Comments { get; set; }
        public PropertyDefinition Party { get; set; }
        public PropertyDefinition MeasurementId { get; set; }
        public PropertyDefinition DischargeUnitId { get; set; }
        public PropertyDefinition AreaUnitId { get; set; }
        public PropertyDefinition AreaValue { get; set; }
        public PropertyDefinition DistanceUnitId { get; set; }
        public PropertyDefinition WidthValue { get; set; }
        public PropertyDefinition VelocityUnitId { get; set; }
        public PropertyDefinition VelocityValue { get; set; }
        public PropertyDefinition MeanIndexVelocity { get; set; }
        public PropertyDefinition ShowInDataCorrection { get; set; }
        public PropertyDefinition ShowInRatingDevelopment { get; set; }
        public PropertyDefinition PreventAutomaticPublishing { get; set; }
        public PropertyDefinition AdjustmentType { get; set; }
        public PropertyDefinition AdjustmentAmount { get; set; }
        public PropertyDefinition ReasonForAdjustment { get; set; }
        public PropertyDefinition GradeCode { get; set; }
        public PropertyDefinition GradeName { get; set; }
        public PropertyDefinition UncertaintyType { get; set; }
        public PropertyDefinition QualityAssuranceComments { get; set; }
        public PropertyDefinition QuantitativeUncertainty { get; set; }
        public PropertyDefinition QualitativeUncertainty { get; set; }
        public List<GageHeightMeasurement> GageHeightMeasurements { get; set; } = new List<GageHeightMeasurement>();
    }

    public class GageHeightMeasurement : ActivityColumnDefinition
    {
        // Default property is GageHeight.Value
        public PropertyDefinition Include { get; set; }
    }

    public class AdcpDischargeDefinition : DischargeActivityColumnDefinition
    {
        // Default property is AdcpDischargeSection.TotalDischarge.Value
        public PropertyDefinition SectionDischarge { get; set; }
        public PropertyDefinition DeviceType { get; set; }
        public PropertyDefinition DeploymentMethod { get; set; }
        public PropertyDefinition MeterSuspension { get; set; }
        public PropertyDefinition NumberOfTransects { get; set; }
        public PropertyDefinition PercentageOfDischargeMeasured { get; set; }
        public PropertyDefinition TransducerDepth { get; set; }
        public PropertyDefinition TopEstimateExponent { get; set; }
        public PropertyDefinition TopEstimateMethod { get; set; }
        public PropertyDefinition BottomEstimateExponent { get; set; }
        public PropertyDefinition BottomEstimateMethod { get; set; }
        public PropertyDefinition NavigationMethod { get; set; }
        public PropertyDefinition FirmwareVersion { get; set; }
        public PropertyDefinition SoftwareVersion { get; set; }
        public PropertyDefinition DepthReference { get; set; }
        public PropertyDefinition MeasurementDeviceManufacturer { get; set; }
        public PropertyDefinition MeasurementDeviceModel { get; set; }
        public PropertyDefinition MeasurementDeviceSerialNumber { get; set; }
    }

    public class ManualGaugingDischargeDefinition : DischargeActivityColumnDefinition
    {
        // Default property is ManualGaugingDischargeSection.TotalDischarge.Value
        public PropertyDefinition SectionDischarge { get; set; }
        public PropertyDefinition DischargeMethod { get; set; }
        public PropertyDefinition PointVelocityObservationType { get; set; }
        public PropertyDefinition DeploymentMethod { get; set; }
        public PropertyDefinition MeterSuspension { get; set; }
        public PropertyDefinition MeterCalibrationManufacturer { get; set; }
        public PropertyDefinition MeterCalibrationModel { get; set; }
        public PropertyDefinition MeterCalibrationSerialNumber { get; set; }
        public PropertyDefinition MeterCalibrationFirmwareVersion { get; set; }
        public PropertyDefinition MeterCalibrationSoftwareVersion { get; set; }
        public PropertyDefinition MeterType { get; set; }
        public List<MeterCalibrationEquationColumnDefinition> MeterCalibrationEquations { get; set; } = new List<MeterCalibrationEquationColumnDefinition>();
    }

    public class MeterCalibrationEquationColumnDefinition : ColumnDefinition
    {
        // Default property is MeterCalibrationEquation.Slope
        public PropertyDefinition RangeStart { get; set; }
        public PropertyDefinition RangeEnd { get; set; }
        public PropertyDefinition Intercept { get; set; }
        public PropertyDefinition InterceptUnitId { get; set; }
    }

    public enum TimestampType
    {
        Unknown,
        DateOnly,
        TimeOnly,
        DateTimeOnly,
        DateTimeOffset,
        DateAndSurvey123Offset,
    }
}
