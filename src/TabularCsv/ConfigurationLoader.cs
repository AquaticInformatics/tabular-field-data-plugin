using System;
using System.IO;
using System.Linq;
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

                configuration.AllowUnusedDefaultProperty();
                configuration.Visit.AllowUnusedDefaultProperty();

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
                        .FromToml(ConvertFixedValueShorthandSyntax)))
                .ConfigurePropertyMapping(m => m
                    .OnTargetPropertyNotFound(WhenTargetPropertyNotFound))
            );

            return settings;
        }

        private void WhenTargetPropertyNotFound(string[] tomlKeyChain, object targetObject, TomlObject targetValue)
        {
            var lineNumber = GuessSourceLine(tomlKeyChain, targetValue);

            var propertyName = $"{targetObject.GetType().FullName?.Replace($"{nameof(TabularCsv)}.", string.Empty)}.{string.Join(".", tomlKeyChain)}";

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
            var valueTarget = targetValue.ToString();

            for (var i = 0; i < lines.Length; ++i)
            {
                var line = lines[i];

                if (line.Contains(keyTarget) && line.Contains(valueTarget))
                    return 1 + i;
            }

            return 0;
        }

        private PropertyDefinition ConvertFixedValueShorthandSyntax(ITomlRoot root, TomlString tomlString)
        {
            return new PropertyDefinition
            {
                FixedValue = tomlString.Value
            };
        }

        private static bool IsEmpty(Configuration configuration)
        {
            return configuration.Id == null
                   && configuration.Visit == null
                   && configuration.ControlCondition == null
                   && !configuration.AllReadings.Any()
                   && !configuration.AllInspections.Any()
                   && !configuration.AllCalibrations.Any()
                   && !configuration.AllAdcpDischarges.Any()
                   && !configuration.AllPanelDischargeSummaries.Any();
        }
    }
}
