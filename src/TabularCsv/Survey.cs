using System.Collections.Generic;
using System.Linq;

namespace TabularCsv
{
    public class Survey
    {
        public string Name { get; set; }
        public int Priority { get; set; }
        public bool FirstLineIsHeader { get; set; }
        public PropertyDefinition Location { get; set; }
        public PropertyDefinition Weather { get; set; }
        public PropertyDefinition CollectionAgency { get; set; }
        public List<MergingTextColumnDefinition> Comments { get; set; } = new List<MergingTextColumnDefinition>();
        public List<MergingTextColumnDefinition> Party { get; set; } = new List<MergingTextColumnDefinition>();
        public List<TimestampColumnDefinition> TimestampColumns { get; set; } = new List<TimestampColumnDefinition>();
        public List<ReadingColumnDefinition> Readings { get; set; } = new List<ReadingColumnDefinition>();
        public List<InspectionColumnDefinition> Inspections { get; set; } = new List<InspectionColumnDefinition>();
        public List<CalibrationColumnDefinition> Calibrations { get; set; } = new List<CalibrationColumnDefinition>();
        public ControlConditionColumnDefinition ControlCondition { get; set; }

        public List<ColumnDefinition> GetColumnDefinitions()
        {
            var timestampColumns = TimestampColumns ?? new List<TimestampColumnDefinition>();
            var commentColumns = Comments ?? new List<MergingTextColumnDefinition>();
            var partyColumns = Party ?? new List<MergingTextColumnDefinition>();
            var readingColumns = Readings ?? new List<ReadingColumnDefinition>();
            var inspectionColumns = Inspections ?? new List<InspectionColumnDefinition>();
            var calibrationColumns = Calibrations ?? new List<CalibrationColumnDefinition>();

            return new ColumnDefinition[]
                {
                    Location,
                    Weather,
                    CollectionAgency,
                }
                .Concat(timestampColumns)
                .Concat(timestampColumns.SelectMany(tc => tc.GetColumnDefinitions()))
                .Concat(commentColumns)
                .Concat(partyColumns)
                .Concat(readingColumns)
                .Concat(readingColumns.SelectMany(rc => rc.GetColumnDefinitions()))
                .Concat(inspectionColumns)
                .Concat(inspectionColumns.SelectMany(rc => rc.GetColumnDefinitions()))
                .Concat(calibrationColumns)
                .Concat(calibrationColumns.SelectMany(rc => rc.GetColumnDefinitions()))
                .Concat(new []{ControlCondition})
                .Where(columnDefinition => columnDefinition != null)
                .ToList();
        }
    }

    public class PropertyDefinition : ColumnDefinition
    {
    }

    public abstract class ColumnDefinition
    {
        public int? ColumnIndex { get; set; }
        public string ColumnHeader { get; set; }
        public string FixedValue { get; set; }

        public bool RequiresHeader()
        {
            return string.IsNullOrEmpty(FixedValue)
                && !string.IsNullOrEmpty(ColumnHeader);
        }

        public string Name()
        {
            return RequiresHeader()
                ? ColumnHeader
                : string.IsNullOrEmpty(FixedValue)
                    ? $"ColumnIndex[{ColumnIndex}]"
                    : $"FixedValue='{FixedValue}'";
        }
    }

    public class MergingTextColumnDefinition : ColumnDefinition
    {
        public string Prefix { get; set; }
    }

    public class TimestampColumnDefinition : ColumnDefinition
    {
        public string Format { get; set; }
        public TimestampType Type { get; set; }
        public PropertyDefinition UtcOffset { get; set; }

        public IEnumerable<ColumnDefinition> GetColumnDefinitions()
        {
            return new ColumnDefinition[]
            {
                this,
                UtcOffset
            };
        }
    }

    public abstract class ActivityColumnDefinition : ColumnDefinition
    {
        public List<TimestampColumnDefinition> TimestampColumns { get; set; } = new List<TimestampColumnDefinition>();

        public virtual IEnumerable<ColumnDefinition> GetColumnDefinitions()
        {
            var timestampColumns = TimestampColumns ?? new List<TimestampColumnDefinition>();

            return new ColumnDefinition[]
                {
                    this,
                }
                .Concat(timestampColumns)
                .Concat(timestampColumns.SelectMany(tc => tc.GetColumnDefinitions()));
        }
    }

    public class ReadingColumnDefinition : ActivityColumnDefinition
    {
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

        public override IEnumerable<ColumnDefinition> GetColumnDefinitions()
        {
            return base.GetColumnDefinitions()
                .Concat(new ColumnDefinition[]
                {
                    ParameterId,
                    UnitId,
                    ReadingType,
                    Comments,
                    GradeCode,
                    GradeName,
                    MeasurementDetailsCut,
                    MeasurementDetailsHold,
                    MeasurementDetailsTapeCorrection,
                    MeasurementDetailsWaterLevel,
                    MeasurementDeviceManufacturer,
                    MeasurementDeviceModel,
                    MeasurementDeviceSerialNumber,
                    Method,
                    Publish,
                    ReadingQualifiers,
                    ReadingQualifierSeparators,
                    ReferencePointName,
                    SubLocation,
                    SensorUniqueId,
                    Uncertainty,
                    UseLocationDatumAsReference,
                });
        }
    }

    public class InspectionColumnDefinition : ActivityColumnDefinition
    {
        public PropertyDefinition Comments { get; set; }
        public PropertyDefinition SubLocation { get; set; }
        public PropertyDefinition MeasurementDeviceManufacturer { get; set; }
        public PropertyDefinition MeasurementDeviceModel { get; set; }
        public PropertyDefinition MeasurementDeviceSerialNumber { get; set; }

        public override IEnumerable<ColumnDefinition> GetColumnDefinitions()
        {
            return base.GetColumnDefinitions()
                .Concat(new ColumnDefinition[]
                {
                    Comments,
                    MeasurementDeviceManufacturer,
                    MeasurementDeviceModel,
                    MeasurementDeviceSerialNumber,
                    SubLocation,
                });
        }
    }

    public class CalibrationColumnDefinition : ActivityColumnDefinition
    {
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
        public TimestampColumnDefinition StandardDetailsExpirationDate { get; set; }
        public PropertyDefinition StandardDetailsTemperature { get; set; }

        public override IEnumerable<ColumnDefinition> GetColumnDefinitions()
        {
            return base.GetColumnDefinitions()
                .Concat(new ColumnDefinition[]
                {
                    ParameterId,
                    UnitId,
                    Comments,
                    Party,
                    CalibrationType,
                    StandardDetailsLotNumber,
                    StandardDetailsStandardCode,
                    StandardDetailsExpirationDate,
                    StandardDetailsTemperature,
                    MeasurementDeviceManufacturer,
                    MeasurementDeviceModel,
                    MeasurementDeviceSerialNumber,
                    Method,
                    Publish,
                    SubLocation,
                    SensorUniqueId,
                    Standard,
                });
        }
    }

    public class ControlConditionColumnDefinition : ActivityColumnDefinition
    {
        public PropertyDefinition UnitId { get; set; }
        public PropertyDefinition Comments { get; set; }
        public PropertyDefinition Party { get; set; }
        public PropertyDefinition ControlCleanedType { get; set; }
        public PropertyDefinition ControlCode { get; set; }
        public PropertyDefinition ConditionType { get; set; }

        public override IEnumerable<ColumnDefinition> GetColumnDefinitions()
        {
            return base.GetColumnDefinitions()
                .Concat(new ColumnDefinition[]
                {
                    UnitId,
                    Comments,
                    Party,
                    ControlCleanedType,
                    ControlCode,
                    ConditionType,
                });
        }
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
