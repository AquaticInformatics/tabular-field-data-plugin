using System;
using System.Collections.Generic;
using System.Linq;
using FieldDataPluginFramework.Context;
using Humanizer;

namespace TabularCsv
{
    public class ConfigurationValidator
    {
        public LocationInfo LocationInfo { get; set; }
        public Configuration Configuration { get; set; }

        public void Validate()
        {
            if (LocationInfo == null && Configuration.Location == null)
                ThrowConfigurationException($"A {nameof(Configuration.Location)} definition is required.");

            var columnDefinitions = Configuration.GetColumnDefinitions();

            var invalidColumns = columnDefinitions
                .Where(column => column.IsInvalid(out _))
                .ToList();

            string ShowInvalidColumn(ColumnDefinition column)
            {
                column.IsInvalid(out var validationMessage);
                return $"{column.Name()}: {validationMessage}";
            }

            if (invalidColumns.Any())
                ThrowConfigurationException($"{"invalid column definitions".ToQuantity(invalidColumns.Count)}:\n{string.Join("\n", invalidColumns.Select(ShowInvalidColumn))}");

            var headerColumns = columnDefinitions
                .Where(column => column.RequiresColumnHeader())
                .ToList();

            if (!Configuration.IsHeaderRowRequired && headerColumns.Any())
                ThrowConfigurationException(
                    $"{"column".ToQuantity(headerColumns.Count)} require a header line, but {nameof(Configuration.IsHeaderRowRequired)} is false: {string.Join(",", headerColumns.Select(column => $"'{column.ColumnHeader}'"))}");

            var invalidIndexedColumns = columnDefinitions
                .Where(column => column.ColumnIndex.HasValue && column.ColumnIndex <= 0)
                .ToList();

            if (invalidIndexedColumns.Any())
                ThrowConfigurationException(
                    $"{"column".ToQuantity(invalidIndexedColumns.Count)} have invalid {nameof(ColumnDefinition.ColumnIndex)} values <= 0.");
        }

        private void ThrowConfigurationException(string message)
        {
            throw new Exception($"Configuration '{Configuration.Id}' is invalid: {message}");
        }

        public Dictionary<string,int> BuildColumnHeaderHeaderMap(string[] headerFields)
        {
            var columnDefinitions = Configuration.GetColumnDefinitions();

            var headerColumns = columnDefinitions
                .Where(column => column.RequiresColumnHeader())
                .ToList();

            var unknownHeaderColumns = headerColumns
                .Where(column => !headerFields.Any(field => field.Equals(column.ColumnHeader, StringComparison.InvariantCultureIgnoreCase)))
                .ToList();

            var unknownHeadersMessage =
                $"{"missing column".ToQuantity(unknownHeaderColumns.Count)}: {string.Join(", ", unknownHeaderColumns.Select(column => column.Name()))}";

            if (unknownHeaderColumns.Count == headerColumns.Count)
                throw new AllHeadersMissingException(unknownHeadersMessage);

            if (unknownHeaderColumns.Any())
                ThrowConfigurationException(unknownHeadersMessage);

            var indexedColumns = columnDefinitions
                .Where(c => c.HasIndexedColumn)
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
