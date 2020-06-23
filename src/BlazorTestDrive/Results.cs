using System.Collections.Generic;
using System.Linq;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.DataModel.Calibrations;
using FieldDataPluginFramework.DataModel.ControlConditions;
using FieldDataPluginFramework.DataModel.DischargeActivities;
using FieldDataPluginFramework.DataModel.Inspections;
using FieldDataPluginFramework.DataModel.LevelSurveys;
using FieldDataPluginFramework.DataModel.Readings;

namespace BlazorTestDrive
{
    public class Results
    {
        public List<FieldVisitInfo> Visits { get; } = new List<FieldVisitInfo>();
        public List<(string VisitIdentifier, Reading Item)> Readings { get; } = new List<(string VisitIdentifier, Reading Item)>();
        public List<(string VisitIdentifier, Inspection Item)> Inspections { get; } = new List<(string VisitIdentifier, Inspection Item)>();
        public List<(string VisitIdentifier, Calibration Item)> Calibrations { get; } = new List<(string VisitIdentifier, Calibration Item)>();
        public List<(string VisitIdentifier, ControlCondition Item)> ControlConditions { get; } = new List<(string VisitIdentifier, ControlCondition Item)>();
        public List<(string VisitIdentifier, DischargeActivity Item)> Discharges { get; } = new List<(string VisitIdentifier, DischargeActivity Item)>();
        public List<(string VisitIdentifier, LevelSurvey Item)> LevelSurveys { get; } = new List<(string VisitIdentifier, LevelSurvey Item)>();

        public static Results CreateResults(AppendedResults appendedResults)
        {
            return new Results(appendedResults);
        }

        private Results(AppendedResults appendedResults)
        {
            var visits = appendedResults.AppendedVisits;

            Visits.AddRange(visits);

            Readings.AddRange(visits
                .SelectMany(v => v.Readings.Select(item => (v.FieldVisitIdentifier, item))));

            Inspections.AddRange(visits
                .SelectMany(v => v.Inspections.Select(item => (v.FieldVisitIdentifier, item))));

            Calibrations.AddRange(visits
                .SelectMany(v => v.Calibrations.Select(item => (v.FieldVisitIdentifier, item))));

            ControlConditions.AddRange(visits
                .SelectMany(v => v.ControlConditions.Select(item => (v.FieldVisitIdentifier, item))));

            Discharges.AddRange(visits
                .SelectMany(v => v.DischargeActivities.Select(item => (v.FieldVisitIdentifier, item))));

            LevelSurveys.AddRange(visits
                .SelectMany(v => v.LevelSurveys.Select(item => (v.FieldVisitIdentifier, item))));
        }
    }
}
