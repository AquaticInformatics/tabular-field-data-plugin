using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using FieldDataPluginFramework;
using Nett;
using Nett.Parser;

namespace TabularCsv
{
    public class ConfigurationLoader
    {
        public ILog Log { get; set; }

        private string TomlText { get; set; }

        public Configuration Load(string name, string tomlText)
        {
            if (string.IsNullOrWhiteSpace(tomlText))
                return null;

            TomlText = tomlText;

            var configuration = LoadFromToml(name);

            if (configuration == null || IsEmpty(configuration))
                return null;

            return configuration;
        }

        private Configuration LoadFromToml(string configurationName)
        {
            var settings = CreateTomlSettings();

            try
            {
                var configuration = Toml.ReadString<Configuration>(TomlText, settings);

                // Set the name to the configuration if none is specified
                configuration.Id = configuration.Id ?? configurationName;

                if (configuration.Visit == null)
                {
                    configuration.Visit = new VisitDefinition();
                }

                configuration.Aliases = CreateCaseInsensitiveAliases(configuration.Aliases);

                return configuration;
            }
            catch (Exception exception)
            {
                var parseException = GetParseException(exception);

                if (parseException != null)
                    throw new ConfigurationException($"Invalid configuration: {configurationName}: {parseException.Message}");

                throw;
            }
        }

        private static ParseException GetParseException(Exception exception)
        {
            if (exception is ParseException parseException)
                return parseException;

            if (exception.InnerException is ParseException innerException)
                return innerException;

            return null;
        }

        private Dictionary<string, Dictionary<string, string>> CreateCaseInsensitiveAliases(
            Dictionary<string, Dictionary<string, string>> aliases)
        {
            if (aliases == null)
                return new Dictionary<string, Dictionary<string, string>>(StringComparer.CurrentCultureIgnoreCase);

            return aliases
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp
                        .Value
                        .ToDictionary(
                            inner => inner.Key,
                            inner => inner.Value,
                            StringComparer.CurrentCultureIgnoreCase),
                    StringComparer.CurrentCultureIgnoreCase);
        }

        private TomlSettings CreateTomlSettings()
        {
            var settings = TomlSettings.Create(s => s
                .ConfigurePropertyMapping(m => m
                    .UseTargetPropertySelector(standardSelectors => standardSelectors.IgnoreCase))
                .ConfigureType<DateOnlyDefinition>(type => type
                    .WithConversionFor<TomlString>(convert => convert
                        .FromToml(ConvertShorthandDateOnlySyntax)))
                .ConfigureType<TimeOnlyDefinition>(type => type
                    .WithConversionFor<TomlString>(convert => convert
                        .FromToml(ConvertShorthandTimeOnlySyntax)))
                .ConfigureType<TimestampDefinition>(type => type
                    .WithConversionFor<TomlString>(convert => convert
                        .FromToml(ConvertShorthandTimestampSyntax)))
                .ConfigureType<List<TimestampDefinition>>(type => type
                    .WithConversionFor<TomlTableArray>(convert => convert
                        .FromToml(ConvertTimeStampList)))
                .ConfigureType<PropertyDefinition>(type => type
                    .WithConversionFor<TomlString>(convert => convert
                        .FromToml(ConvertShorthandPropertySyntax)))
                .ConfigureType<PropertyDefinition>(type => type
                    .WithConversionFor<TomlInt>(convert => convert
                        .FromToml(ConvertShorthandColumnIndex)))
                .ConfigureType<Regex>(type => type
                    .WithConversionFor<TomlString>(convert => convert
                        .FromToml(ConvertRegexFromString)))
                .ConfigurePropertyMapping(m => m
                    .OnTargetPropertyNotFound(WhenTargetPropertyNotFound))
            );

            return settings;
        }

        private void WhenTargetPropertyNotFound(string[] tomlKeyChain, object targetObject, TomlObject targetValue)
        {
            var lineNumber = GuessSourceLine(tomlKeyChain, targetValue);

            var targetType = targetObject
                .GetType();

            var typeName = targetType
                .FullName
                ?.Replace($"{nameof(TabularCsv)}.", string.Empty);

            var propertyName = $"{typeName}.{string.Join(".", tomlKeyChain)}";

            var targetPropertyName = tomlKeyChain.LastOrDefault();
            var targetSectionName = tomlKeyChain.Length > 1
                ? tomlKeyChain[tomlKeyChain.Length - 2]
                : default;

            var message = $"'{propertyName}' is not a known property name.";

            if (!string.IsNullOrEmpty(targetPropertyName) && !string.IsNullOrEmpty(targetSectionName))
                message = $"'{targetPropertyName}' is not a known [{targetSectionName}] property name.";

            if (!string.IsNullOrEmpty(targetPropertyName))
            {
                var bestGuess = BestGuess(
                    targetPropertyName,
                    targetType
                        .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.SetProperty)
                        .Where(p => !ExcludedPropertyNames.Contains(p.Name)),
                    propertyInfo => propertyInfo.Name);

                if (!string.IsNullOrEmpty(bestGuess))
                    message = $"{message} {bestGuess}";
            }

            message = $"{message} See https://github.com/AquaticInformatics/tabular-field-data-plugin/wiki/Configuration-Fields for details.";

            if (lineNumber.HasValue)
                throw new ParseException($"Line {lineNumber}: {message}");

            throw new ParseException(message);
        }

        private static readonly HashSet<string> ExcludedPropertyNames = new HashSet<string>
        {
            nameof(Configuration.Aliases),
            nameof(Configuration.AllAdcpDischarges),
            nameof(Configuration.AllCalibrations),
            nameof(Configuration.AllEndTimes),
            nameof(Configuration.AllEngineeredStructureDischarges),
            nameof(Configuration.AllInspections),
            nameof(Configuration.AllLevelSurveys),
            nameof(Configuration.AllMergeWithComments),
            nameof(Configuration.AllOtherDischarges),
            nameof(Configuration.AllPanelDischargeSummaries),
            nameof(Configuration.AllReadings),
            nameof(Configuration.AllStartTimes),
            nameof(Configuration.AllTimes),
            nameof(Configuration.AllVolumetricDischarges),
        };

        public static string BestGuess<TItem>(string target, IEnumerable<TItem> items, Func<TItem, string> selector, int maximumGuessDistance = 7, int maximumGuesses = 4)
        {
            var lev = new Fastenshtein.Levenshtein(target);

            var orderedItems = items
                .Select(item => (Item: item, Text: selector(item), Distance: lev.DistanceFrom(selector(item))))
                .OrderBy(tuple => tuple.Distance)
                .ToList();

            if (!orderedItems.Any())
                return string.Empty;

            var best = orderedItems.First();

            if (best.Distance > maximumGuessDistance)
                return string.Empty;

            var guesses = orderedItems
                .Where(tuple => tuple.Distance <= best.Distance + 1)
                .OrderBy(tuple => tuple.Distance)
                .ThenByDescending(tuple => tuple.Text.StartsWith(target, StringComparison.InvariantCultureIgnoreCase))
                .ThenByDescending(tuple => tuple.Text.EndsWith(target, StringComparison.InvariantCultureIgnoreCase))
                .ToList();

            if (guesses.Count > maximumGuesses)
                guesses = orderedItems
                    .Where(tuple => tuple.Distance == best.Distance)
                    .OrderByDescending(tuple => tuple.Text.StartsWith(target, StringComparison.InvariantCultureIgnoreCase))
                    .ThenByDescending(tuple => tuple.Text.EndsWith(target, StringComparison.InvariantCultureIgnoreCase))
                    .ToList();

            if (!guesses.Any() || guesses.Count > maximumGuesses)
                return string.Empty;

            switch (guesses.Count)
            {
                case 1:
                    return $"Did you mean '{guesses.Single().Text}'?";

                case 2:
                    return $"Did you mean '{guesses[0].Text}' or '{guesses[1].Text}'?";

                default:
                    return $"Did you mean '{string.Join("', or '", guesses.Select(tuple => tuple.Text))}'?";
            }
        }

        private int? GuessSourceLine(string[] tomlKeyChain, TomlObject targetValue)
        {
            if (tomlKeyChain.Length < 1)
                return null;

            var lines = TomlText.Split(
                new[] { "\r\n", "\r", "\n" },
                StringSplitOptions.None
            );

            var keyTarget = tomlKeyChain.Last();
            var valueTarget = SimpleTomlTypes.Contains(targetValue.TomlType)
                ? targetValue.ToString()
                : null;

            for (var i = 0; i < lines.Length; ++i)
            {
                var line = lines[i];

                if (line.Contains(keyTarget) && (valueTarget == null || line.Contains(valueTarget)))
                    return 1 + i;
            }

            return null;
        }

        private static readonly HashSet<TomlObjectType> SimpleTomlTypes = new HashSet<TomlObjectType>
        {
            TomlObjectType.Bool,
            TomlObjectType.Int,
            TomlObjectType.Float,
            TomlObjectType.String,
        };

        private List<TimestampDefinition> ConvertTimeStampList(ITomlRoot root, TomlTableArray tomlTableArray)
        {
            return tomlTableArray
                .Items
                .Select(t => CreateTimestampFromTable(root, t))
                .ToList();
        }

        private TimestampDefinition CreateTimestampFromTable(ITomlRoot root, TomlTable tomlTable)
        {
            var property = CreatePropertyFromTable(root, tomlTable);

            var timestamp = new TimestampDefinition
            {
                FixedValue = property.FixedValue,
                ColumnHeader = property.ColumnHeader,
                ColumnIndex = property.ColumnIndex,
                PrefaceRegex = property.PrefaceRegex,
                Alias = property.Alias,
            };

            if (TryGetValue<TomlString>(tomlTable, nameof(TimestampDefinition.Type), out var tomlString)
                && Enum.TryParse<TimestampType>(tomlString.Value, true, out var type))
            {
                timestamp.Type = type;
            }

            if (TryGetValue(tomlTable, nameof(TimestampDefinition.Formats), out tomlString))
            {
                timestamp.Formats = new[] {tomlString.Value};
            }
            else if (TryGetValue<TomlArray>(tomlTable, nameof(TimestampDefinition.Formats), out var tomlArray))
            {
                timestamp.Formats = tomlArray
                    .Items
                    .Select(item => item.Get<string>())
                    .ToArray();
            }

            if (tomlTable.TryGetValue(nameof(TimestampDefinition.UtcOffset), out var tomlObject))
                timestamp.UtcOffset = CreatePropertyFromObject(root, tomlObject);

            return timestamp;
        }

        private PropertyDefinition CreatePropertyFromObject(ITomlRoot root, TomlObject tomlObject)
        {
            if (tomlObject is TomlString tomlString)
                return ConvertShorthandPropertySyntax(root, tomlString);

            if (tomlObject is TomlTable tomlTable)
            {
                return CreatePropertyFromTable(root, tomlTable);
            }

            throw new ArgumentException($"Can't convert type '{tomlObject.TomlType}' to a {nameof(PropertyDefinition)}");
        }

        private PropertyDefinition CreatePropertyFromTable(ITomlRoot root, TomlTable tomlTable)
        {
            var definition = new PropertyDefinition();

            if (TryGetValue<TomlString>(tomlTable, nameof(PropertyDefinition.FixedValue), out var tomlString))
                definition.FixedValue = tomlString.Value;

            if (TryGetValue(tomlTable, nameof(PropertyDefinition.ColumnHeader), out tomlString))
                definition.ColumnHeader = tomlString.Value;

            if (TryGetValue<TomlInt>(tomlTable, nameof(PropertyDefinition.ColumnIndex), out var tomlInt))
                definition.ColumnIndex = (int)tomlInt.Value;

            if (TryGetValue(tomlTable, nameof(PropertyDefinition.PrefaceRegex), out tomlString))
                definition.PrefaceRegex = ConvertRegexFromString(root, tomlString);


            if (TryGetValue(tomlTable, nameof(PropertyDefinition.Alias), out tomlString))
                definition.Alias = tomlString.Value;

            return definition;
        }

        private bool TryGetValue<TTomlType>(TomlTable tomlTable, string key, out TTomlType value) where TTomlType : TomlValue
        {
            value = null;

            if (tomlTable.TryGetValue(key, out var tomlObject) && tomlObject is TTomlType valueObject)
            {
                value = valueObject;
                return true;
            }

            return false;
        }

        private TimestampDefinition ConvertShorthandTimestampSyntax(ITomlRoot root, TomlString tomlString)
        {
            return ParseTimestampDefinitionFromShorthand(tomlString, TimestampType.DateTimeOnly);
        }

        private TimeOnlyDefinition ConvertShorthandTimeOnlySyntax(ITomlRoot root, TomlString tomlString)
        {
            return ParseTimestampDefinitionFromShorthand(tomlString, TimestampType.TimeOnly, () => new TimeOnlyDefinition());
        }

        private DateOnlyDefinition ConvertShorthandDateOnlySyntax(ITomlRoot root, TomlString tomlString)
        {
            return ParseTimestampDefinitionFromShorthand(tomlString, TimestampType.DateOnly, () => new DateOnlyDefinition());
        }

        private TDefinition ParseTimestampDefinitionFromShorthand<TDefinition>(TomlString tomlString, TimestampType defaultTimestampType, Func<TDefinition> factory)
            where TDefinition : TimestampBaseDefinition
        {
            var temp = ParseTimestampDefinitionFromShorthand(tomlString, defaultTimestampType);

            var definition = factory();

            definition.FixedValue = temp.FixedValue;
            definition.ColumnHeader = temp.ColumnHeader;
            definition.ColumnIndex = temp.ColumnIndex;
            definition.PrefaceRegex = temp.PrefaceRegex;
            definition.Alias = temp.Alias;
            definition.Formats = temp.Formats;
            definition.UtcOffset = temp.UtcOffset;
            definition.Type = temp.Type;

            return definition;
        }

        private TimestampDefinition ParseTimestampDefinitionFromShorthand(TomlString tomlString, TimestampType defaultTimestampType)
        {
            var text = tomlString.Value;

            var match = TimestampPropertyRegex.Match(text);

            if (!match.Success)
                throw new ArgumentException($"'{text}' is not a supported timestamp string syntax.");

            var property = ParsePropertyDefinition(match.Groups["property"].Value);

            var timestamp = new TimestampDefinition
            {
                FixedValue = property.FixedValue,
                ColumnHeader = property.ColumnHeader,
                ColumnIndex = property.ColumnIndex,
                PrefaceRegex = property.PrefaceRegex,
                Alias = property.Alias,
            };

            foreach (var capture in match.Groups["timestampOption"].Captures.Cast<Capture>())
            {
                var optionText = capture.Value.Trim();

                if (string.IsNullOrEmpty(optionText))
                    continue;

                if (Enum.TryParse<TimestampType>(optionText, true, out var timestampType))
                {
                    if (timestamp.Type.HasValue)
                        throw new ArgumentException($"{nameof(timestamp.Type)} is already set to {timestamp.Type} and cannot be changed to {timestampType}.");

                    timestamp.Type = timestampType;
                }
                else if (TryParseUtcOffset(optionText, out var timeSpan))
                {
                    if (timestamp.UtcOffset != null)
                        throw new ArgumentException($"{nameof(timestamp.UtcOffset)} is already set to {timestamp.UtcOffset.Name()} and cannot be changed to '{optionText}'.");

                    timestamp.UtcOffset = new PropertyDefinition
                    {
                        FixedValue = $"{timeSpan}"
                    };
                }
                else
                {
                    timestamp.Formats = new List<string>(timestamp.Formats ?? new string[0])
                        .Concat(new[] {optionText})
                        .ToArray();
                }
            }

            if (!timestamp.Type.HasValue)
                timestamp.Type = defaultTimestampType;

            return timestamp;
        }

        public static bool TryParseUtcOffset(string text, out TimeSpan timeSpan)
        {
            timeSpan = TimeSpan.Zero;

            const string utcPrefix = "UTC";

            if (text.StartsWith(utcPrefix, StringComparison.InvariantCultureIgnoreCase))
            {
                var timeSpanText = text.Substring(utcPrefix.Length);

                if (string.IsNullOrEmpty(timeSpanText))
                    return true;

                if (timeSpanText.StartsWith("+"))
                    timeSpanText = timeSpanText.Substring(1);

                if (!timeSpanText.Contains(":"))
                    timeSpanText += ":00";

                if (TimeSpan.TryParse(timeSpanText, CultureInfo.InvariantCulture, out timeSpan))
                    return true;
            }

            return false;
        }

        private PropertyDefinition ConvertShorthandColumnIndex(ITomlRoot root, TomlInt tomlInt)
        {
            var columnIndex = tomlInt.Value;

            if (columnIndex <= 0 || columnIndex > int.MaxValue)
                throw new ParseException($"{columnIndex} is an invalid {nameof(ColumnDefinition.ColumnIndex)} value. Must be > 0.");

            return new PropertyDefinition
            {
                ColumnIndex = (int) columnIndex
            };
        }

        private PropertyDefinition ConvertShorthandPropertySyntax(ITomlRoot root, TomlString tomlString)
        {
            var text = tomlString.Value;

            return ParsePropertyDefinition(text);
        }

        private static PropertyDefinition ParsePropertyDefinition(string text)
        {
            string fixedValue = null;
            string columnHeader = null;
            Regex prefaceRegex = null;
            int? columnIndex = null;
            string alias = null;

            var match = PropertyWithAliasRegex.Match(text);

            if (match.Success)
            {
                alias = ValueOrNull(match.Groups["alias"].Value.Trim());

                var columnText = ValueOrNull(match.Groups["columnHeader"].Value.Trim());

                if (!string.IsNullOrEmpty(columnText))
                {
                    if (TryParseInteger(columnText, out var indexValue))
                    {
                        columnIndex = indexValue;
                    }
                    else
                    {
                        columnHeader = columnText;
                    }
                }
                else
                {
                    prefaceRegex = CreateRegex(
                        ValueOrNull(match.Groups["regexPattern"].Value),
                        ValueOrNull(match.Groups["regexOptions"].Value.Trim()));
                }
            }
            else
            {
                fixedValue = text;
            }

            return new PropertyDefinition
            {
                FixedValue = fixedValue,
                ColumnHeader = columnHeader,
                ColumnIndex = columnIndex,
                PrefaceRegex = prefaceRegex,
                Alias = alias
            };
        }

        private static string ValueOrNull(string text)
        {
            return string.IsNullOrEmpty(text)
                ? null
                : text;
        }

        private static bool TryParseInteger(string text, out int value)
        {
            value = 0;

            if (!text.StartsWith("#"))
                return false;

            text = text.Substring(1).Trim();

            if (int.TryParse(text, out value))
                return true;

            var match = ExcelColumnShorthandRegex.Match(text);

            if (!match.Success)
                return false;

            value = ConvertExcelColumnToIndex(match.Groups["columnName"].Value.Trim());
            return true;
        }

        private static readonly Regex InternalPropertyRegex = new Regex(
            @"(@(?<columnHeader>[^{|]+)|/(?<regexPattern>.+)/(?<regexOptions>[imsx]*(-[imsx]+)?))(\s*\{\s*(?<alias>[^}]+)\s*})?",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly Regex PropertyWithAliasRegex = new Regex(
            $@"^{InternalPropertyRegex}$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex ExcelColumnShorthandRegex = new Regex(
            @"^\s*(?<columnName>[A-Z]+)\s*$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly Regex TimestampPropertyRegex = new Regex(
            $@"^(?<property>{InternalPropertyRegex})(\s*\|(?<timestampOption>[^|]+))*$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static int ConvertExcelColumnToIndex(string columnName)
        {
            return columnName
                .ToUpperInvariant()
                .Aggregate(0, (column, letter) => 26 * column + letter - 'A' + 1);
        }

        public static string ConvertOneBasedIndexToExcelColumn(int columnIndex)
        {
            var columnName = "";

            while (columnIndex > 0)
            {
                var modulo = (columnIndex - 1) % 26;
                columnName = Convert.ToChar('A' + modulo) + columnName;
                columnIndex = (columnIndex - modulo) / 26;
            }

            return columnName;
        }

        private Regex ConvertRegexFromString(ITomlRoot root, TomlString tomlString)
        {
            var text = tomlString.Value;

            var match = PropertyWithAliasRegex.Match(text);

            if (!match.Success)
                return new Regex(text);

            var pattern = match.Groups["regexPattern"].Value;
            var optionsText = match.Groups["regexOptions"].Value;

            return CreateRegex(pattern, optionsText);
        }

        private static Regex CreateRegex(string pattern, string optionsText)
        {
            if (string.IsNullOrEmpty(pattern))
                return null;

            var options = RegexOptions.IgnoreCase;
            var addOption = true;

            foreach (var ch in optionsText?.ToLowerInvariant() ?? string.Empty)
            {
                if (ch == '-')
                {
                    addOption = false;
                    continue;
                }

                if (!SupportedFlags.TryGetValue(ch, out var option))
                    continue;

                if (addOption)
                    options |= option;
                else
                    options &= ~option;
            }

            return new Regex(pattern, options);
        }

        private static readonly Dictionary<char, RegexOptions> SupportedFlags = new Dictionary<char, RegexOptions>
        {
            {'i', RegexOptions.IgnoreCase},
            {'m', RegexOptions.Multiline},
            {'s', RegexOptions.Singleline},
            {'x', RegexOptions.IgnorePatternWhitespace},
        };

        private static bool IsEmpty(Configuration configuration)
        {
            return configuration.Id == null
                   && configuration.ControlCondition == null
                   && !configuration.AllReadings.Any()
                   && !configuration.AllInspections.Any()
                   && !configuration.AllCalibrations.Any()
                   && !configuration.AllAdcpDischarges.Any()
                   && !configuration.AllPanelDischargeSummaries.Any()
                   && !configuration.AllLevelSurveys.Any();
        }
    }
}
