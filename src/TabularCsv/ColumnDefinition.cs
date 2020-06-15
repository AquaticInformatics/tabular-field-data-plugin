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
        public Regex PrefaceRegex { get; set; }
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
                   && !HasPrefaceRegex
                   && HasNamedColumn;
        }

        public bool HasFixedValue => !string.IsNullOrEmpty(FixedValue);
        public bool HasNamedColumn => !string.IsNullOrWhiteSpace(ColumnHeader);
        public bool HasIndexedColumn => ColumnIndex.HasValue;
        public bool HasPrefaceRegex => PrefaceRegex != null;
        public bool HasMultilineRegex => HasPrefaceRegex && 0 != (PrefaceRegex.Options & (RegexOptions.Multiline | RegexOptions.Singleline));
        public bool HasAlias => !string.IsNullOrEmpty(Alias);

        public bool IsInvalid(out string validationMessage)
        {
            validationMessage = null;

            if (HasPrefaceRegex)
            {
                if (!PrefaceRegex.GetGroupNames().Select(n => n.ToLowerInvariant()).Contains(RegexCaptureGroupName))
                {
                    validationMessage = $"A named capture group is missing. Use something like: (?<{RegexCaptureGroupName}>PATTERN)";

                    return true;
                }
            }

            var setProperties = new[]
                {
                    HasFixedValue ? nameof(FixedValue) : null,
                    HasPrefaceRegex ? nameof(PrefaceRegex) : null,
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
                    nameof(PrefaceRegex),
                };

                var setPropertyContext = setProperties.Any()
                    ? $": {string.Join(", ", setProperties)}"
                    : string.Empty;

                validationMessage = setProperties.Any()
                    ? $"Only one of the {string.Join(", ", allProperties)} properties can be set. You have set {setProperties.Count} properties{setPropertyContext}."
                    : $"You must set exactly one of the {string.Join(", ", allProperties)} properties.";

                return true;
            }

            return false;
        }

        public string Name()
        {
            var suffix = RequiresColumnHeader()
                ? $"{nameof(ColumnHeader)}='{ColumnHeader}'"
                : HasFixedValue
                    ? $"{nameof(FixedValue)}='{FixedValue}'"
                    : HasPrefaceRegex
                        ? $"{nameof(PrefaceRegex)}='{PrefaceRegex}'"
                        : HasIndexedColumn
                            ? $"{nameof(ColumnIndex)}[{ColumnIndex}]"
                            : "NoContextSpecified";

            return $"{NamePrefix}.{suffix}";
        }
    }
}