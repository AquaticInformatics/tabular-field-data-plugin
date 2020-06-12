using System.Linq;
using System.Text.RegularExpressions;

namespace TabularCsv
{
    public abstract class ColumnDefinition
    {
        public const string RegexCaptureGroupName = "value";

        public int? ColumnIndex { get; set; }
        public string ColumnHeader { get; set; }
        public string FixedValue { get; set; }
        public Regex HeaderRegex { get; set; }
        public string Alias { get; set; }

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
        public bool HasHeaderRegex => HeaderRegex != null;
        public bool HasMultilineRegex => HasHeaderRegex && 0 != (HeaderRegex.Options & (RegexOptions.Multiline | RegexOptions.Singleline));
        public bool HasAlias => !string.IsNullOrEmpty(Alias);

        public bool IsInvalid(out string validationMessage)
        {
            validationMessage = null;

            if (HasHeaderRegex)
            {
                if (!HeaderRegex.GetGroupNames().Contains(RegexCaptureGroupName))
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
}