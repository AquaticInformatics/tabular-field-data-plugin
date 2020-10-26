using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.DataModel;
using FieldDataPluginFramework.DataModel.Calibrations;
using FieldDataPluginFramework.DataModel.ChannelMeasurements;
using FieldDataPluginFramework.DataModel.ControlConditions;
using FieldDataPluginFramework.DataModel.DischargeActivities;
using FieldDataPluginFramework.DataModel.GageZeroFlow;
using FieldDataPluginFramework.DataModel.Inspections;
using FieldDataPluginFramework.DataModel.LevelSurveys;
using FieldDataPluginFramework.DataModel.Readings;
using FieldDataPluginFramework.Results;

namespace TabularCsv
{
    // The delayed appender class exists to solely to delay the creation of visits until merges have been resolved.
    // This will allow a visit to be created, then expanded (a widened Start or End timestamp) to include more activities.
    public class DelayedAppender : IDisposable
    {
        public class InternalConstructor<T> where T : class
        {
            public static T Invoke(params object[] args)
            {
                return Activator.CreateInstance(typeof(T), BindingFlags.NonPublic | BindingFlags.Instance, null, args, null) as T;
            }
        }

        private IFieldDataResultsAppender ActualAppender { get; set;  }

        public DelayedAppender(IFieldDataResultsAppender actualAppender)
        {
            ActualAppender = actualAppender;
        }

        public void Dispose()
        {
            if (ActualAppender == null)
                return;

            AppendAllResults();
            ActualAppender = null;
        }

        private void AppendAllResults()
        {
            foreach (var delayedFieldVisit in DelayedFieldVisits)
            {
                AppendDelayedVisit(delayedFieldVisit);
            }
        }

        public void AdjustVisitPeriodToContainAllActivities(FieldVisitInfo visit)
        {
            var times = GetActivityTimes(visit)
                .Where(t => t != DateTimeOffset.MinValue && t != DateTimeOffset.MaxValue)
                .OrderBy(t => t)
                .ToList();

            if (!times.Any())
                return;

            var start = times.First();
            var end = times.Last();

            if (start == end)
            {
                // Avoid AQ-26134 in pre-2020.2
                end = end.AddMinutes(1);

                if (start.Date != end.Date)
                {
                    // Extend the start instead, so that the date remains the same
                    end = start;
                    start = start.AddMinutes(-1);
                }
            }

            if (visit.StartDate == start && visit.EndDate == end)
                return;

            visit.FieldVisitDetails.FieldVisitPeriod = new DateTimeInterval(start, end);
        }

        private IEnumerable<DateTimeOffset> GetActivityTimes(FieldVisitInfo visit)
        {
            return new DateTimeOffset?[]
                {
                    visit.StartDate,
                    visit.EndDate,
                }
                .Concat(visit.Readings.SelectMany(GetTimes))
                .Concat(visit.Inspections.SelectMany(GetTimes))
                .Concat(visit.Calibrations.SelectMany(GetTimes))
                .Concat(visit.LevelSurveys.SelectMany(GetTimes))
                .Concat(visit.DischargeActivities.SelectMany(GetTimes))
                .Concat(visit.GageZeroFlowActivities.SelectMany(GetTimes))
                .Where(dt => dt.HasValue)
                .Select(dt => dt.Value);
        }

        private IEnumerable<DateTimeOffset?> GetTimes(Reading item)
        {
            return new[]
            {
                item.DateTimeOffset
            };
        }

        private IEnumerable<DateTimeOffset?> GetTimes(Inspection item)
        {
            return new[]
            {
                item.DateTimeOffset
            };
        }

        private IEnumerable<DateTimeOffset?> GetTimes(Calibration item)
        {
            return new DateTimeOffset?[0]; // Because calibration times can occur outside the visit range
        }

        private IEnumerable<DateTimeOffset?> GetTimes(LevelSurvey item)
        {
            return item
                .LevelSurveyMeasurements
                .Select(m => (DateTimeOffset?) m.MeasurementTime);
        }

        private IEnumerable<DateTimeOffset?> GetTimes(DischargeActivity item)
        {
            return new DateTimeOffset?[]
                {
                    item.MeasurementStartTime,
                    item.MeasurementEndTime,
                }
                .Concat(item.ChannelMeasurements.SelectMany(GetTimes))
                .Concat(item.GageHeightMeasurements.Select(g => g.MeasurementTime));
        }

        private IEnumerable<DateTimeOffset?> GetTimes(ChannelMeasurementBase item)
        {
            return new DateTimeOffset?[]
            {
                item.MeasurementStartTime,
                item.MeasurementEndTime,
            };
        }

        private IEnumerable<DateTimeOffset?> GetTimes(GageZeroFlowActivity item)
        {
            return new DateTimeOffset?[]
            {
                item.ObservationDate,
            };
        }

        private void AppendDelayedVisit(FieldVisitInfo delayedVisit)
        {
            var visit = ActualAppender.AddFieldVisit(delayedVisit.LocationInfo, delayedVisit.FieldVisitDetails);

            foreach (var dischargeActivity in delayedVisit.DischargeActivities)
            {
                ActualAppender.AddDischargeActivity(visit, dischargeActivity);
            }

            foreach (var reading in delayedVisit.Readings)
            {
                ActualAppender.AddReading(visit, reading);
            }

            foreach (var calibration in delayedVisit.Calibrations)
            {
                ActualAppender.AddCalibration(visit, calibration);
            }

            foreach (var inspection in delayedVisit.Inspections)
            {
                ActualAppender.AddInspection(visit, inspection);
            }

            foreach (var controlCondition in delayedVisit.ControlConditions)
            {
                ActualAppender.AddControlCondition(visit, controlCondition);
            }

            foreach (var gageZeroFlowActivity in delayedVisit.GageZeroFlowActivities)
            {
                ActualAppender.AddGageZeroFlowActivity(visit, gageZeroFlowActivity);
            }

            foreach (var levelSurvey in delayedVisit.LevelSurveys)
            {
                ActualAppender.AddLevelSurvey(visit, levelSurvey);
            }

            foreach (var crossSectionSurvey in delayedVisit.CrossSectionSurveys)
            {
                ActualAppender.AddCrossSectionSurvey(visit, crossSectionSurvey);
            }
        }

        public LocationInfo GetLocationByIdentifier(string locationIdentifier)
        {
            return ActualAppender.GetLocationByIdentifier(locationIdentifier);
        }

        public LocationInfo GetLocationByUniqueId(string uniqueId)
        {
            return ActualAppender.GetLocationByUniqueId(uniqueId);
        }

        private List<FieldVisitInfo> DelayedFieldVisits { get; } = new List<FieldVisitInfo>();

        public bool AnyResultsAppended => DelayedFieldVisits.Any();

        public FieldVisitInfo AddFieldVisit(LocationInfo location, FieldVisitDetails fieldVisitDetails)
        {
            var existingVisit = DelayedFieldVisits
                .FirstOrDefault(visit => visit.LocationInfo.LocationIdentifier == location.LocationIdentifier &&
                                         DoPeriodsOverlap(visit.FieldVisitDetails.FieldVisitPeriod, fieldVisitDetails.FieldVisitPeriod));

            if (existingVisit != null)
                return existingVisit;

            var fieldVisitInfo = InternalConstructor<FieldVisitInfo>.Invoke(location, fieldVisitDetails);

            DelayedFieldVisits.Add(fieldVisitInfo);

            return fieldVisitInfo;
        }

        private static bool DoPeriodsOverlap(DateTimeInterval earlierPeriod, DateTimeInterval laterPeriod)
        {
            if (laterPeriod.Start < earlierPeriod.Start)
            {
                // Ensure earlierPeriod precedes laterPeriod, for simpler comparision
                var tempPeriod = earlierPeriod;
                earlierPeriod = laterPeriod;
                laterPeriod = tempPeriod;
            }

            var earlierEnd = earlierPeriod.End;

            var laterStart = laterPeriod.Start;

            if (earlierEnd < laterStart)
                return false;

            if (earlierEnd > laterStart)
                return true;

            return false;
        }

        public void AddDischargeActivity(FieldVisitInfo fieldVisit, DischargeActivity dischargeActivity)
        {
            fieldVisit.DischargeActivities.Add(dischargeActivity);
        }

        public void AddReading(FieldVisitInfo fieldVisit, Reading reading)
        {
            fieldVisit.Readings.Add(reading);
        }

        public void AddLevelSurvey(FieldVisitInfo fieldVisit, LevelSurvey levelSurvey)
        {
            fieldVisit.LevelSurveys.Add(levelSurvey);
        }

        public void AddControlCondition(FieldVisitInfo fieldVisit, ControlCondition controlCondition)
        {
            fieldVisit.ControlConditions.Add(controlCondition);
        }

        public void AddInspection(FieldVisitInfo fieldVisit, Inspection inspection)
        {
            fieldVisit.Inspections.Add(inspection);
        }

        public void AddCalibration(FieldVisitInfo fieldVisit, Calibration calibration)
        {
            fieldVisit.Calibrations.Add(calibration);
        }

        public void AddGageZeroFlowActivity(FieldVisitInfo fieldVisit, GageZeroFlowActivity gageZeroFlowActivity)
        {
            fieldVisit.GageZeroFlowActivities.Add(gageZeroFlowActivity);
        }

        public Dictionary<string, string> GetPluginConfigurations()
        {
            return ActualAppender.GetPluginConfigurations();
        }
    }
}
