using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TabularCsv
{
    public class Configuration : TimeRangeActivityDefinition
    {
        public string Id { get; set; }
        public int Priority { get; set; } = 1;
        public string Separator { get; set; }
        public string LocaleName { get; set; }
        public int PrefaceRowCount { get; set; }
        public string PrefaceEndsWith { get; set; }
        public string PrefaceEndsBefore { get; set; }
        public string PrefaceMustContain { get; set; }
        public Regex PrefaceMustMatchRegex { get; set; }
        public int MaximumPrefaceLines { get; set; } = 50;
        public int HeaderRowCount { get; set; }
        public bool NoDataRowsExpected { get; set; }
        public string CommentLinePrefix { get; set; }
        public bool StrictMode { get; set; } = true;

        public Dictionary<string, Dictionary<string,string>> Aliases { get; set; } = new Dictionary<string, Dictionary<string, string>>();

        public PropertyDefinition Location { get; set; }

        public VisitDefinition Visit { get; set; }
        public ControlConditionDefinition ControlCondition { get; set; }

        public ReadingDefinition Reading { get; set; }
        public List<ReadingDefinition> Readings { get; set; } = new List<ReadingDefinition>();
        public List<ReadingDefinition> AllReadings => AllDefinitions(Reading, Readings);

        public InspectionDefinition Inspection { get; set; }
        public List<InspectionDefinition> Inspections { get; set; } = new List<InspectionDefinition>();
        public List<InspectionDefinition> AllInspections => AllDefinitions(Inspection, Inspections);

        public CalibrationDefinition Calibration { get; set; }
        public List<CalibrationDefinition> Calibrations { get; set; } = new List<CalibrationDefinition>();
        public List<CalibrationDefinition> AllCalibrations => AllDefinitions(Calibration, Calibrations);

        public AdcpDischargeDefinition AdcpDischarge { get; set; }
        public List<AdcpDischargeDefinition> AdcpDischarges { get; set; } = new List<AdcpDischargeDefinition>();
        public List<AdcpDischargeDefinition> AllAdcpDischarges => AllDefinitions(AdcpDischarge, AdcpDischarges);

        public ManualGaugingDischargeDefinition PanelDischargeSummary { get; set; }
        public List<ManualGaugingDischargeDefinition> PanelDischargeSummaries { get; set; } = new List<ManualGaugingDischargeDefinition>();
        public List<ManualGaugingDischargeDefinition> AllPanelDischargeSummaries => AllDefinitions(PanelDischargeSummary, PanelDischargeSummaries);

        public LevelSurveyDefinition LevelSurvey { get; set; }
        public List<LevelSurveyDefinition> LevelSurveys { get; set; } = new List<LevelSurveyDefinition>();
        public List<LevelSurveyDefinition> AllLevelSurveys => AllDefinitions(LevelSurvey, LevelSurveys);

        public bool IsDisabled => Priority <= 0;

        public bool IsPrefaceExpected => PrefaceRowCount > 0
                                               || !string.IsNullOrEmpty(PrefaceEndsWith)
                                               || !string.IsNullOrEmpty(PrefaceEndsBefore)
                                               || !string.IsNullOrEmpty(PrefaceMustContain)
                                               || PrefaceMustMatchRegex != null
                                               || GetColumnDefinitions().Any(c => c.HasPrefaceRegex);

        public bool IsHeaderRowRequired => HeaderRowCount > 0
                                           || GetColumnDefinitions().Any(c => c.RequiresColumnHeader());

        private List<ColumnDefinition> ColumnDefinitions { get; set; }

        public List<ColumnDefinition> GetColumnDefinitions()
        {
            if (ColumnDefinitions == null)
            {
                ColumnDefinitions = ColumnDecorator.GetNamedColumns(this);
            }

            return ColumnDefinitions;
        }
    }

    public class PropertyDefinition : ColumnDefinition
    {
    }

    public class TimestampDefinition : ColumnDefinition
    {
        public string[] Formats { get; set; }
        public TimestampType? Type { get; set; }
        public PropertyDefinition UtcOffset { get; set; }
    }

    public abstract class CoreDefinition
    {
        public PropertyDefinition Comment { get; set; }
        public PropertyDefinition MergeWithComment { get; set; }
        public List<PropertyDefinition> MergeWithComments { get; set; } = new List<PropertyDefinition>();
        public List<PropertyDefinition> AllMergeWithComments => AllDefinitions(MergeWithComment, MergeWithComments);

        protected List<TDefinition> AllDefinitions<TDefinition>(TDefinition item, IEnumerable<TDefinition> items)
            where TDefinition : class
        {
            return new List<TDefinition>
                {
                    item
                }
                .Concat(items)
                .Where(i => i != null)
                .ToList();
        }
    }

    public abstract class ActivityDefinition : CoreDefinition
    {
        public TimestampDefinition Time { get; set; }
        public List<TimestampDefinition> Times { get; set; } = new List<TimestampDefinition>();
        public List<TimestampDefinition> AllTimes => AllDefinitions(Time, Times);
    }

    public abstract class TimeRangeActivityDefinition : ActivityDefinition
    {
        public TimestampDefinition StartTime { get; set; }
        public List<TimestampDefinition> StartTimes { get; set; } = new List<TimestampDefinition>();
        public List<TimestampDefinition> AllStartTimes => AllDefinitions(StartTime, StartTimes);

        public TimestampDefinition EndTime { get; set; }
        public List<TimestampDefinition> EndTimes { get; set; } = new List<TimestampDefinition>();
        public List<TimestampDefinition> AllEndTimes => AllDefinitions(EndTime, EndTimes);
    }

    public class VisitDefinition : TimeRangeActivityDefinition
    {
        public PropertyDefinition Weather { get; set; }
        public PropertyDefinition CollectionAgency { get; set; }
        public PropertyDefinition CompletedGroundWaterLevels { get; set; }
        public PropertyDefinition CompletedLevelSurvey { get; set; }
        public PropertyDefinition CompletedRecorderData { get; set; }
        public PropertyDefinition CompletedSafetyInspection { get; set; }
        public PropertyDefinition CompletedOtherSample { get; set; }
        public PropertyDefinition CompletedBiologicalSample { get; set; }
        public PropertyDefinition CompletedSedimentSample { get; set; }
        public PropertyDefinition CompletedWaterQualitySample { get; set; }
        public PropertyDefinition Party { get; set; }
    }

    public class ReadingDefinition : ActivityDefinition
    {
        public PropertyDefinition Value { get; set; }
        public PropertyDefinition ParameterId { get; set; }
        public PropertyDefinition UnitId { get; set; }
        public PropertyDefinition ReadingType { get; set; }
        public PropertyDefinition GradeCode { get; set; }
        public PropertyDefinition GradeName { get; set; }
        public PropertyDefinition Method { get; set; }
        public PropertyDefinition Publish { get; set; }
        public PropertyDefinition ReferencePointName { get; set; }
        public PropertyDefinition SensorUniqueId { get; set; }
        public PropertyDefinition SubLocation { get; set; }
        public PropertyDefinition UseLocationDatumAsReference { get; set; }
        public PropertyDefinition Uncertainty { get; set; }
        public PropertyDefinition MeasurementDeviceManufacturer { get; set; }
        public PropertyDefinition MeasurementDeviceModel { get; set; }
        public PropertyDefinition MeasurementDeviceSerialNumber { get; set; }
        public PropertyDefinition ReadingQualifiers { get; set; }
        public PropertyDefinition ReadingQualifierSeparators { get; set; }
        public PropertyDefinition MeasurementDetailsCut { get; set; }
        public PropertyDefinition MeasurementDetailsHold { get; set; }
        public PropertyDefinition MeasurementDetailsTapeCorrection { get; set; }
        public PropertyDefinition MeasurementDetailsWaterLevel { get; set; }
    }

    public class InspectionDefinition : ActivityDefinition
    {
        public PropertyDefinition InspectionType { get; set; }
        public PropertyDefinition SubLocation { get; set; }
        public PropertyDefinition MeasurementDeviceManufacturer { get; set; }
        public PropertyDefinition MeasurementDeviceModel { get; set; }
        public PropertyDefinition MeasurementDeviceSerialNumber { get; set; }
    }

    public class CalibrationDefinition : ActivityDefinition
    {
        public PropertyDefinition Value { get; set; }
        public PropertyDefinition ParameterId { get; set; }
        public PropertyDefinition UnitId { get; set; }
        public PropertyDefinition Party { get; set; }
        public PropertyDefinition CalibrationType { get; set; }
        public PropertyDefinition Method { get; set; }
        public PropertyDefinition Publish { get; set; }
        public PropertyDefinition SensorUniqueId { get; set; }
        public PropertyDefinition SubLocation { get; set; }
        public PropertyDefinition Standard { get; set; }
        public PropertyDefinition MeasurementDeviceManufacturer { get; set; }
        public PropertyDefinition MeasurementDeviceModel { get; set; }
        public PropertyDefinition MeasurementDeviceSerialNumber { get; set; }
        public PropertyDefinition StandardDetailsLotNumber { get; set; }
        public PropertyDefinition StandardDetailsStandardCode { get; set; }
        public TimestampDefinition StandardDetailsExpirationDate { get; set; }
        public PropertyDefinition StandardDetailsTemperature { get; set; }
    }

    public class ControlConditionDefinition : ActivityDefinition
    {
        public PropertyDefinition ConditionType { get; set; }
        public PropertyDefinition UnitId { get; set; }
        public PropertyDefinition Party { get; set; }
        public PropertyDefinition ControlCleanedType { get; set; }
        public PropertyDefinition ControlCode { get; set; }
        public PropertyDefinition DistanceToGage { get; set; }
    }

    public abstract class DischargeActivityDefinition : TimeRangeActivityDefinition
    {
        public PropertyDefinition TotalDischarge { get; set; }
        public PropertyDefinition ChannelName { get; set; }
        public PropertyDefinition Party { get; set; }
        public PropertyDefinition MeasurementId { get; set; }
        public PropertyDefinition DischargeUnitId { get; set; }
        public PropertyDefinition AreaUnitId { get; set; }
        public PropertyDefinition AreaValue { get; set; }
        public PropertyDefinition DistanceUnitId { get; set; }
        public PropertyDefinition WidthValue { get; set; }
        public PropertyDefinition VelocityUnitId { get; set; }
        public PropertyDefinition MeanIndexVelocity { get; set; }
        public PropertyDefinition ShowInDataCorrection { get; set; }
        public PropertyDefinition ShowInRatingDevelopment { get; set; }
        public PropertyDefinition PreventAutomaticPublishing { get; set; }
        public PropertyDefinition AdjustmentType { get; set; }
        public PropertyDefinition AdjustmentAmount { get; set; }
        public PropertyDefinition ReasonForAdjustment { get; set; }
        public PropertyDefinition GradeCode { get; set; }
        public PropertyDefinition GradeName { get; set; }
        public PropertyDefinition UncertaintyType { get; set; }
        public PropertyDefinition QualityAssuranceComments { get; set; }
        public PropertyDefinition QuantitativeUncertainty { get; set; }
        public PropertyDefinition QualitativeUncertainty { get; set; }
        public PropertyDefinition MeanGageHeightDurationHours { get; set; }
        public PropertyDefinition ManuallyCalculatedMeanGageHeight { get; set; }
        public PropertyDefinition MeanGageHeightDifferenceDuringVisit { get; set; }

        public GageHeightMeasurementDefinition GageHeightMeasurement { get; set; }
        public List<GageHeightMeasurementDefinition> GageHeightMeasurements { get; set; } = new List<GageHeightMeasurementDefinition>();
        public List<GageHeightMeasurementDefinition> AllGageHeightMeasurements => AllDefinitions(GageHeightMeasurement, GageHeightMeasurements);
    }

    public class GageHeightMeasurementDefinition : ActivityDefinition
    {
        public PropertyDefinition Value { get; set; }
        public PropertyDefinition Include { get; set; }
    }

    public class AdcpDischargeDefinition : DischargeActivityDefinition
    {
        public PropertyDefinition SectionDischarge { get; set; }
        public PropertyDefinition DeviceType { get; set; }
        public PropertyDefinition DeploymentMethod { get; set; }
        public PropertyDefinition MeterSuspension { get; set; }
        public PropertyDefinition NumberOfTransects { get; set; }
        public PropertyDefinition MagneticVariation { get; set; }
        public PropertyDefinition DischargeCoefficientVariation { get; set; }
        public PropertyDefinition PercentageOfDischargeMeasured { get; set; }
        public PropertyDefinition TransducerDepth { get; set; }
        public PropertyDefinition VelocityAverageValue { get; set; }
        public PropertyDefinition TopEstimateExponent { get; set; }
        public PropertyDefinition TopEstimateMethod { get; set; }
        public PropertyDefinition BottomEstimateExponent { get; set; }
        public PropertyDefinition BottomEstimateMethod { get; set; }
        public PropertyDefinition NavigationMethod { get; set; }
        public PropertyDefinition FirmwareVersion { get; set; }
        public PropertyDefinition SoftwareVersion { get; set; }
        public PropertyDefinition DepthReference { get; set; }
        public PropertyDefinition MeasurementDeviceManufacturer { get; set; }
        public PropertyDefinition MeasurementDeviceModel { get; set; }
        public PropertyDefinition MeasurementDeviceSerialNumber { get; set; }
    }

    public class ManualGaugingDischargeDefinition : DischargeActivityDefinition
    {
        public PropertyDefinition SectionDischarge { get; set; }
        public PropertyDefinition StartPoint { get; set; }
        public PropertyDefinition DischargeMethod { get; set; }
        public PropertyDefinition VelocityAverageValue { get; set; }
        public PropertyDefinition VelocityObservationMethod { get; set; }
        public PropertyDefinition DeploymentMethod { get; set; }
        public PropertyDefinition NumberOfVerticals { get; set; }
        public PropertyDefinition MeterSuspension { get; set; }
        public PropertyDefinition MeterCalibrationManufacturer { get; set; }
        public PropertyDefinition MeterCalibrationModel { get; set; }
        public PropertyDefinition MeterCalibrationSerialNumber { get; set; }
        public PropertyDefinition MeterCalibrationConfiguration { get; set; }
        public PropertyDefinition MeterCalibrationFirmwareVersion { get; set; }
        public PropertyDefinition MeterCalibrationSoftwareVersion { get; set; }
        public PropertyDefinition MeterType { get; set; }

        public MeterCalibrationEquationDefinition MeterCalibrationEquation { get; set; }
        public List<MeterCalibrationEquationDefinition> MeterCalibrationEquations { get; set; } = new List<MeterCalibrationEquationDefinition>();
        public List<MeterCalibrationEquationDefinition> AllMeterCalibrationEquations => AllDefinitions(MeterCalibrationEquation, MeterCalibrationEquations);
    }

    public class MeterCalibrationEquationDefinition : ColumnDefinition
    {
        public PropertyDefinition Slope { get; set; }
        public PropertyDefinition RangeStart { get; set; }
        public PropertyDefinition RangeEnd { get; set; }
        public PropertyDefinition Intercept { get; set; }
        public PropertyDefinition InterceptUnitId { get; set; }
    }

    public class LevelSurveyDefinition : CoreDefinition
    {
        public PropertyDefinition OriginReferencePointName { get; set; }
        public PropertyDefinition Party { get; set; }
        public PropertyDefinition Method { get; set; }

        public LevelSurveyMeasurementDefinition LevelSurveyMeasurement { get; set; }
        public List<LevelSurveyMeasurementDefinition> LevelSurveyMeasurements { get; set; } = new List<LevelSurveyMeasurementDefinition>();
        public List<LevelSurveyMeasurementDefinition> AllLevelSurveyMeasurements => AllDefinitions(LevelSurveyMeasurement, LevelSurveyMeasurements);
    }

    public class LevelSurveyMeasurementDefinition : ActivityDefinition
    {
        public PropertyDefinition MeasuredElevation { get; set; }
        public PropertyDefinition ReferencePointName { get; set; }
    }
}
