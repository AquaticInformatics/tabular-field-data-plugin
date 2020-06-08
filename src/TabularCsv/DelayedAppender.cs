using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.DataModel;
using FieldDataPluginFramework.DataModel.ControlConditions;
using FieldDataPluginFramework.DataModel.CrossSection;
using FieldDataPluginFramework.DataModel.DischargeActivities;
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
            if (fieldVisit.Readings.Any(r => AreEquivalent(r, reading)))
                return;

            fieldVisit.Readings.Add(reading);
        }

        private static bool AreEquivalent(Reading r1, Reading r2)
        {
            return r1.DateTimeOffset == r2.DateTimeOffset
                   && r1.ParameterId == r2.ParameterId
                   && r1.Measurement.UnitId == r2.Measurement.UnitId
                   // ReSharper disable once CompareOfFloatsByEqualityOperator
                   && r1.Measurement.Value == r2.Measurement.Value
                   && r1.ReadingType == r2.ReadingType;
        }

        public void AddCrossSectionSurvey(FieldVisitInfo fieldVisit, CrossSectionSurvey crossSectionSurvey)
        {
            fieldVisit.CrossSectionSurveys.Add(crossSectionSurvey);
        }

        public void AddLevelSurvey(FieldVisitInfo fieldVisit, LevelSurvey levelSurvey)
        {
            fieldVisit.LevelSurveys.Add(levelSurvey);
        }

        public void AddControlCondition(FieldVisitInfo fieldVisit, ControlCondition controlCondition)
        {
            fieldVisit.ControlConditions.Add(controlCondition);
        }
    }
}
