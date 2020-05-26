using System;
using System.Collections.Generic;
using FieldDataPluginFramework.DataModel.Readings;

namespace Survey123
{
    public class Survey
    {
        public string Name { get; set; }
        public bool FirstLineIsHeader { get; set; }
        public ColumnDefinition LocationColumn { get; set; }
        public List<MergingTextColumnDefinition> CommentColumns { get; set; } = new List<MergingTextColumnDefinition>();
        public List<MergingTextColumnDefinition> PartyColumns { get; set; } = new List<MergingTextColumnDefinition>();
        public List<TimestampColumnDefinition> TimestampColumns { get; set; } = new List<TimestampColumnDefinition>();
        public List<ReadingColumnDefinition> ReadingColumns { get; set; } = new List<ReadingColumnDefinition>();
        public Dictionary<string,string> LocationAliases { get; set; } = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
    }

    public class ColumnDefinition
    {
        public int? ColumnIndex { get; set; }
        public string ColumnHeader { get; set; }

        public bool RequiresHeader()
        {
            return !string.IsNullOrEmpty(ColumnHeader);
        }

        public string Name()
        {
            return RequiresHeader()
                ? ColumnHeader
                : $"ColumnIndex[{ColumnIndex}]";
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
        public TimeSpan? UtcOffset { get; set; }
    }

    public class ReadingColumnDefinition : ColumnDefinition
    {
        public string ParameterId { get; set; }
        public string UnitId { get; set; }
        public string CommentPrefix { get; set; }
        public ReadingType? ReadingType { get; set; }
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
