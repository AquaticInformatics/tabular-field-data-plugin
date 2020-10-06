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
            if (Configuration.IsDisabled)
                return;

            if (LocationInfo == null && Configuration.Location == null)
                ThrowConfigurationException($"A {nameof(Configuration.Location)} definition is required.");

            if (Configuration.Separator?.Length > 1)
                ThrowConfigurationException($"The {nameof(Configuration.Separator)} field can only be a single character. '{Configuration.Separator}' is too long.");

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

            var invalidAliasedColumns = columnDefinitions
                .Where(column => column.HasAlias && !Configuration.Aliases.ContainsKey(column.Alias))
                .ToList();

            if (invalidAliasedColumns.Any())
                ThrowConfigurationException(
                    $"No {string.Join(", ", invalidAliasedColumns.Select(c => c.Alias).Distinct().Select(s => $"[Alias.{s}]"))} tables are defined for {"aliased column".ToQuantity(invalidAliasedColumns.Count)}: {string.Join(", ", invalidAliasedColumns.Select(c => $"{c.Name()}(Alias='{c.Alias}')"))}");
        }

        private void ThrowConfigurationException(string message)
        {
            throw new Exception($"Configuration '{Configuration.Id}' is invalid: {message}");
        }

        public Dictionary<string,int> BuildColumnHeaderMap(string[] headerFields)
        {
            var columnDefinitions = Configuration.GetColumnDefinitions();

            var headerColumns = columnDefinitions
                .Where(column => column.RequiresColumnHeader())
                .ToList();

            var unknownHeaderColumns = headerColumns
                .Where(column => !headerFields.Any(field => FieldMatchesColumn(field, column)))
                .ToList();

            var unknownHeadersMessage =
                $"{"missing column".ToQuantity(unknownHeaderColumns.Count)}: {string.Join(", ", unknownHeaderColumns.Select(column => column.Name()))}";

            if (unknownHeaderColumns.Count == headerColumns.Count)
                throw new AllHeadersMissingException(unknownHeadersMessage);

            if (unknownHeaderColumns.Any())
                ThrowConfigurationException(unknownHeadersMessage);

            var duplicateHeaderFields = headerColumns
                .Where(column => headerFields.Count(field => FieldMatchesColumn(field, column)) > 1)
                .Select(column => column.ColumnHeader)
                .Distinct()
                .ToList();

            if (duplicateHeaderFields.Any())
            {
                var duplicateColumnIndexes = duplicateHeaderFields
                    .Select(field =>
                    {
                        var indexes = headerFields
                            .Select((headerField,index) => headerField.Equals(field, StringComparison.CurrentCultureIgnoreCase) ? index + 1 : 0)
                            .Where(index => index > 0)
                            .ToList();

                        return $"'{field}' occurs at {nameof(ColumnDefinition.ColumnIndex)} {string.Join(" and ", indexes)}";
                    })
                    .ToList();

                ThrowConfigurationException(
                    $"The header row has {$"ambiguous {nameof(ColumnDefinition.ColumnHeader)} name".ToQuantity(duplicateHeaderFields.Count)}: {string.Join(", ", duplicateColumnIndexes)}");
            }

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
                .Where(tuple => !string.IsNullOrWhiteSpace(tuple.HeaderValue))
                .ToDictionary(
                    tuple => tuple.HeaderValue,
                    tuple => tuple.HeaderIndex,
                    StringComparer.CurrentCultureIgnoreCase);
        }

        private static bool FieldMatchesColumn(string field, ColumnDefinition column)
        {
            if (field == null || column?.ColumnHeader == null)
                return false;

            return field.Equals(column.ColumnHeader, StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
