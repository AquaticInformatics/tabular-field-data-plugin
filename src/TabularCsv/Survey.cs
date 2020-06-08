using System.Collections.Generic;
using System.Linq;

namespace TabularCsv
{
    public class Survey
    {
        public string Name { get; set; }
        public bool FirstLineIsHeader { get; set; }
        public PropertyDefinition LocationColumn { get; set; }
        public List<MergingTextColumnDefinition> CommentColumns { get; set; } = new List<MergingTextColumnDefinition>();
        public List<MergingTextColumnDefinition> PartyColumns { get; set; } = new List<MergingTextColumnDefinition>();
        public List<TimestampColumnDefinition> TimestampColumns { get; set; } = new List<TimestampColumnDefinition>();
        public List<ReadingColumnDefinition> ReadingColumns { get; set; } = new List<ReadingColumnDefinition>();

        public List<ColumnDefinition> GetColumnDefinitions()
        {
            var timestampColumns = TimestampColumns ?? new List<TimestampColumnDefinition>();
            var commentColumns = CommentColumns ?? new List<MergingTextColumnDefinition>();
            var partyColumns = PartyColumns ?? new List<MergingTextColumnDefinition>();
            var readingColumns = ReadingColumns ?? new List<ReadingColumnDefinition>();

            return new[]
                {
                    (ColumnDefinition)LocationColumn
                }
                .Concat(timestampColumns)
                .Concat(timestampColumns.SelectMany(tc => tc.GetColumnDefinitions()))
                .Concat(commentColumns)
                .Concat(partyColumns)
                .Concat(readingColumns)
                .Concat(readingColumns.SelectMany(rc => rc.GetColumnDefinitions()))
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

    public class ReadingColumnDefinition : ColumnDefinition
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
        public List<TimestampColumnDefinition> TimestampColumns { get; set; } = new List<TimestampColumnDefinition>();

        public IEnumerable<ColumnDefinition> GetColumnDefinitions()
        {
            var timestampColumns = TimestampColumns ?? new List<TimestampColumnDefinition>();

            return new ColumnDefinition[]
                {
                    this,
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
                }
                .Concat(timestampColumns);
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
