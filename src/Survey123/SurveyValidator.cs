using System;
using System.Collections.Generic;
using System.Linq;
using Humanizer;

namespace Survey123
{
    public class SurveyValidator
    {
        public Survey Survey { get; set; }

        public void Validate()
        {
            if (Survey.LocationColumn == null)
                ThrowConfigurationException($"A {nameof(Survey.LocationColumn)} definition is required.");

            if (!Survey.TimestampColumns?.Any() ?? true)
                ThrowConfigurationException($"No {nameof(Survey.TimestampColumns)} definitions were found.");

            if (!Survey.ReadingColumns?.Any() ?? true)
                ThrowConfigurationException($"No {nameof(Survey.ReadingColumns)} definitions were found.");

            var columnDefinitions = GetColumnDefinitions();
            var headerColumns = columnDefinitions
                .Where(column => column.RequiresHeader())
                .ToList();

            if (Survey.FirstLineIsHeader == false && headerColumns.Any())
                ThrowConfigurationException(
                    $"{"column".ToQuantity(headerColumns.Count)} require a header line, but {nameof(Survey.FirstLineIsHeader)} is false: {string.Join(",", headerColumns.Select(column => $"'{column.ColumnHeader}'"))}");

            var invalidIndexedColumns = columnDefinitions
                .Where(column => column.ColumnIndex.HasValue && column.ColumnIndex <= 0)
                .ToList();

            if (invalidIndexedColumns.Any())
                ThrowConfigurationException(
                    $"{"column".ToQuantity(invalidIndexedColumns.Count)} have invalid {nameof(ColumnDefinition.ColumnIndex)} values <= 0.");
        }

        private void ThrowConfigurationException(string message)
        {
            throw new Exception($"{Survey.Name} survey configuration is invalid: {message}");
        }

        private List<ColumnDefinition> GetColumnDefinitions()
        {
            return new[]
                {
                    Survey.LocationColumn
                }
                .Concat(Survey.TimestampColumns ?? new List<TimestampColumnDefinition>())
                .Concat(Survey.CommentColumns ?? new List<MergingTextColumnDefinition>())
                .Concat(Survey.PartyColumns ?? new List<MergingTextColumnDefinition>())
                .Concat(Survey.ReadingColumns ?? new List<ReadingColumnDefinition>())
                .ToList();
        }

        public Dictionary<string,int> BuildHeaderMap(string[] headerFields)
        {
            var columnDefinitions = GetColumnDefinitions();

            var headerColumns = columnDefinitions
                .Where(column => column.RequiresHeader())
                .ToList();

            var unknownHeaderColumns = headerColumns
                .Where(column => !headerFields.Any(field => field.Equals(column.ColumnHeader, StringComparison.InvariantCultureIgnoreCase)))
                .ToList();

            if (unknownHeaderColumns.Any())
                ThrowConfigurationException(
                    $"{"missing column".ToQuantity(unknownHeaderColumns.Count)}: {string.Join(", ", unknownHeaderColumns.Select(column => $"'{column.ColumnHeader}'"))}");

            var indexedColumns = columnDefinitions
                .Where(c => c.ColumnIndex.HasValue)
                .ToList();

            var invalidIndexedColumns = indexedColumns
                .Where(column => column.ColumnIndex > headerFields.Length)
                .ToList();

            if (invalidIndexedColumns.Any())
                ThrowConfigurationException($"{"column".ToQuantity(invalidIndexedColumns.Count)} have {nameof(ColumnDefinition.ColumnIndex)} values > {headerFields.Length}.");

            return headerFields
                .Select((field,i) => (HeaderValue: field, HeaderIndex: i+1))
                .ToDictionary(
                    tuple => tuple.HeaderValue,
                    tuple => tuple.HeaderIndex,
                    StringComparer.InvariantCultureIgnoreCase);
        }
    }
}
