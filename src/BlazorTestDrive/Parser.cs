using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using FieldDataPluginFramework;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.DataModel;
using FieldDataPluginFramework.DataModel.Calibrations;
using FieldDataPluginFramework.DataModel.ControlConditions;
using FieldDataPluginFramework.DataModel.CrossSection;
using FieldDataPluginFramework.DataModel.DischargeActivities;
using FieldDataPluginFramework.DataModel.Inspections;
using FieldDataPluginFramework.DataModel.LevelSurveys;
using FieldDataPluginFramework.DataModel.Readings;
using FieldDataPluginFramework.Results;

namespace BlazorTestDrive
{
    public class Parser
    {
        public FakeLogger Logger { get; } = new FakeLogger();

        public (string Details, Results Results) Parse(string config, string csv,
            string locationIdentifier, string timeZone)
        {
            var plugin = LoadTabularPlugin();

            var appender = new FakeAppender();

            if (TryParseTimeSpan(timeZone, out var utcOffset))
                appender.UtcOffset = utcOffset;

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv)))
            {
                WriteTemporaryConfigFile(config);

                locationIdentifier = locationIdentifier?.Trim();

                var result = string.IsNullOrEmpty(locationIdentifier)
                    ? plugin.ParseFile(stream, appender, Logger)
                    : plugin.ParseFile(stream, appender.GetLocationByIdentifier(locationIdentifier), appender, Logger);

                if (result.Status == ParseFileStatus.SuccessfullyParsedAndDataValid)
                {
                    Logger.Info($"{result.Status}");
                }
                else
                {
                    Logger.Error($"{result.Status}: Parsed={result.Parsed} ErrorMessage={result.ErrorMessage}");
                }

                appender.AppendedResults.PluginAssemblyQualifiedTypeName = plugin.GetType().AssemblyQualifiedName;

                var results = Results.CreateResults(appender.AppendedResults);

                return (Logger.Builder.ToString(), results);
            }
        }

        private void WriteTemporaryConfigFile(string configToml)
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                @"Aquatic Informatics\AQUARIUS Server\Configuration\TabularCSV",
                "config.toml");

            var fileInfo = new FileInfo(path);

            // ReSharper disable once AssignNullToNotNullAttribute
            Directory.CreateDirectory(fileInfo.DirectoryName);

            File.WriteAllText(path, configToml);
        }

        private IFieldDataPlugin LoadTabularPlugin()
        {
            return new TabularCsv.Plugin();
        }

        private bool TryParseTimeSpan(string text, out TimeSpan timeSpan)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                timeSpan = TimeSpan.Zero;
                return false;
            }

            const string prefix = "UTC";

            if (text.StartsWith(prefix, StringComparison.InvariantCultureIgnoreCase))
                text = text.Substring(prefix.Length);

            if (text.StartsWith("+"))
                text = text.Substring(1);

            return TimeSpan.TryParse(text, out timeSpan);
        }
    }

    public class FakeLogger : ILog
    {
        public StringBuilder Builder = new StringBuilder();

        public void Info(string message)
        {
            Builder.AppendLine($"INFO: {message}");
        }

        public void Error(string message)
        {
            Builder.AppendLine($"ERROR: {message}");
        }
    }

    public class InternalConstructor<T> where T : class
    {
        public static T Invoke(params object[] args)
        {
            return Activator.CreateInstance(typeof(T), BindingFlags.NonPublic | BindingFlags.Instance, null, args, null) as T;
        }
    }

    public class AppendedResults
    {
        public string FrameworkAssemblyQualifiedName { get; set; }
        public string PluginAssemblyQualifiedTypeName { get; set; }
        public List<FieldVisitInfo> AppendedVisits { get; set; } = new List<FieldVisitInfo>();
    }

    public class FakeAppender : IFieldDataResultsAppender
    {
        public LocationInfo CreateDummyLocationInfoByIdentifier(string locationIdentifier)
        {
            return CreateDummyLocationInfo(locationIdentifier, $"DummyNameFor-{locationIdentifier}", Guid.Empty);
        }

        private LocationInfo CreateDummyLocationInfoByUniqueId(Guid uniqueId)
        {
            return CreateDummyLocationInfo($"DummyIdentifierFor-{uniqueId:N}", $"DummyNameFor-{uniqueId:N}", uniqueId);
        }

        private LocationInfo CreateDummyLocationInfo(string identifier, string name, Guid uniqueId)
        {
            const long dummyLocationId = 0;

            var locationInfo = InternalConstructor<LocationInfo>.Invoke(
                name,
                identifier,
                dummyLocationId,
                uniqueId,
                UtcOffset.TotalHours);

            if (KnownLocations.Any(l => l.LocationIdentifier == identifier))
                throw new ArgumentException($"Can't add duplicate location for Identifier='{identifier}'");

            KnownLocations.Add(locationInfo);

            return locationInfo;
        }

        private static readonly List<LocationInfo> KnownLocations = new List<LocationInfo>();

        public LocationInfo ForcedLocationInfo { get; set; }
        public TimeSpan UtcOffset { get; set; }

        public AppendedResults AppendedResults { get; } = new AppendedResults
        {
            FrameworkAssemblyQualifiedName = typeof(IFieldDataPlugin).AssemblyQualifiedName
        };

        public LocationInfo GetLocationByIdentifier(string locationIdentifier)
        {
            if (ForcedLocationInfo != null)
            {
                if (ForcedLocationInfo.LocationIdentifier == locationIdentifier)
                    return ForcedLocationInfo;

                throw new ArgumentException($"Location {locationIdentifier} does not exist");
            }

            var locationInfo = KnownLocations.SingleOrDefault(l => l.LocationIdentifier == locationIdentifier);

            return locationInfo ?? CreateDummyLocationInfoByIdentifier(locationIdentifier);
        }

        public LocationInfo GetLocationByUniqueId(string uniqueIdText)
        {
            if (!Guid.TryParse(uniqueIdText, out var uniqueId))
                throw new ArgumentException($"Can't parse '{uniqueIdText}' as a unique ID");

            var locationInfo = KnownLocations.SingleOrDefault(l => Guid.Parse(l.UniqueId) == uniqueId);

            return locationInfo ?? CreateDummyLocationInfoByUniqueId(uniqueId);
        }

        public FieldVisitInfo AddFieldVisit(LocationInfo location, FieldVisitDetails fieldVisitDetails)
        {
            var fieldVisitInfo = InternalConstructor<FieldVisitInfo>.Invoke(location, fieldVisitDetails);

            AppendedResults.AppendedVisits.Add(fieldVisitInfo);

            return fieldVisitInfo;
        }

        public void AddDischargeActivity(FieldVisitInfo fieldVisit, DischargeActivity dischargeActivity)
        {
            fieldVisit.DischargeActivities.Add(dischargeActivity);
        }

        public void AddControlCondition(FieldVisitInfo fieldVisit, ControlCondition controlCondition)
        {
            fieldVisit.ControlConditions.Add(controlCondition);
        }

        public void AddCrossSectionSurvey(FieldVisitInfo fieldVisit, CrossSectionSurvey crossSectionSurvey)
        {
            fieldVisit.CrossSectionSurveys.Add(crossSectionSurvey);
        }

        public void AddReading(FieldVisitInfo fieldVisit, Reading reading)
        {
            fieldVisit.Readings.Add(reading);
        }

        public void AddCalibration(FieldVisitInfo fieldVisit, Calibration calibration)
        {
            fieldVisit.Calibrations.Add(calibration);
        }

        public void AddInspection(FieldVisitInfo fieldVisit, Inspection inspection)
        {
            fieldVisit.Inspections.Add(inspection);
        }

        public void AddLevelSurvey(FieldVisitInfo fieldVisit, LevelSurvey levelSurvey)
        {
            fieldVisit.LevelSurveys.Add(levelSurvey);
        }
    }
}
