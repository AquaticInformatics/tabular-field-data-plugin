using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public Configuration Load(string path)
        {
            if (!File.Exists(path))
                throw new Exception($"'{path}' does not exist.");

            TomlText = File.ReadAllText(path);

            var configuration = LoadFromToml(path);

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

                return configuration;
            }
            catch (ParseException exception)
            {
                throw new ConfigurationException($"Invalid configuration: {configurationName}: {exception.Message}");
            }
        }

        private TomlSettings CreateTomlSettings()
        {
            var settings = TomlSettings.Create(s => s
                .ConfigurePropertyMapping(m => m
                    .UseTargetPropertySelector(standardSelectors => standardSelectors.IgnoreCase))
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

            var typeName = targetObject
                .GetType()
                .FullName
                ?.Replace($"{nameof(TabularCsv)}.", string.Empty);

            var propertyName = $"{typeName}.{string.Join(".", tomlKeyChain)}";

            var message = $"'{propertyName}' is not a valid key.";

            if (lineNumber.HasValue)
                throw new ParseException($"Line {lineNumber}: {message}");

            throw new ParseException(message);
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

            string fixedValue = null;
            string columnHeader = null;
            Regex prefaceRegex = null;
            int? columnIndex = null;
            string alias = null;

            var match = PropertyWithAliasRegex.Match(text);

            if (match.Success)
            {
                alias = ValueOrNull(match.Groups["alias"].Value);

                var columnText = ValueOrNull(match.Groups["columnHeader"].Value);

                if (!string.IsNullOrEmpty(columnText))
                {
                    var excelMatch = ExcelColumnShorthandRegex.Match(columnText);

                    if (excelMatch.Success)
                    {
                        columnIndex = ConvertExcelColumnToIndex(excelMatch.Groups["columnName"].Value);
                    }
                    else if (int.TryParse(columnText, out var index))
                    {
                        columnIndex = index;
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
                        ValueOrNull(match.Groups["regexOptions"].Value));
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

        private static readonly Regex PropertyWithAliasRegex = new Regex(
            @"^(@(?<columnHeader>[^{]+)|/(?<regexPattern>.+)/(?<regexOptions>[imsx]*(-[imsx]+)?))(\s*\{\s*(?<alias>[^}]+)\s*})?$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly Regex ExcelColumnShorthandRegex = new Regex(
            @"^@\s*(?<columnName>[A-Z]+)\s*$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static int ConvertExcelColumnToIndex(string columnName)
        {
            return columnName
                .ToUpperInvariant()
                .Aggregate(0, (column, letter) => 26 * column + letter - 'A' + 1);
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
