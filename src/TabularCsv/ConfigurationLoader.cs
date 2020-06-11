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

        public Configuration Load(string path)
        {
            if (!File.Exists(path))
                throw new Exception($"'{path}' does not exist.");

            var text = File.ReadAllText(path);

            var configuration = LoadFromToml(path, text);

            if (configuration == null || IsEmpty(configuration))
                return null;

            return configuration;
        }

        private Configuration LoadFromToml(string configurationName, string tomlText)
        {
            var settings = CreateTomlSettings();

            try
            {
                var configuration = Toml.ReadString<Configuration>(tomlText, settings);

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
            );

            return settings;
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
