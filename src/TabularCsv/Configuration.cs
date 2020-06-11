using System.Collections.Generic;
using System.Linq;

namespace TabularCsv
{
    public class Configuration : TimeRangeActivityDefinition
    {
        public string Id { get; set; }
        public int Priority { get; set; }
        public string Separator { get; set; }
        public int HeaderRowCount { get; set; }
        public string HeadersEndWith { get; set; }
        public string HeadersEndBefore { get; set; }
        public bool FirstDataRowIsColumnHeader { get; set; }
        public string CommentLinePrefix { get; set; }

        public PropertyDefinition Location { get; set; }

        public VisitDefinition Visit { get; set; }
        public ControlConditionColumnDefinition ControlCondition { get; set; }

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

        public bool IsHeaderSectionExpected => HeaderRowCount > 0
                                               || !string.IsNullOrEmpty(HeadersEndWith)
                                               || !string.IsNullOrEmpty(HeadersEndBefore);

        public bool IsHeaderRowRequired => FirstDataRowIsColumnHeader
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

    public class MergingTextDefinition : ColumnDefinition
    {
        public string Prefix { get; set; }
    }

    public class TimestampDefinition : ColumnDefinition
    {
        public string Format { get; set; }
        public TimestampType Type { get; set; }
        public PropertyDefinition UtcOffset { get; set; }
    }

    public abstract class CoreDefinition : ColumnDefinition
    {
        protected List<TDefinition> AllDefinitions<TDefinition>(TDefinition item, IEnumerable<TDefinition> items)
            where TDefinition : ColumnDefinition
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
        // No default property for the main visit
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
        public List<MergingTextDefinition> Comments { get; set; } = new List<MergingTextDefinition>();
        public List<MergingTextDefinition> Party { get; set; } = new List<MergingTextDefinition>();
    }

    public class ReadingDefinition : ActivityDefinition
    {
        // Default property is Reading.Value
        public PropertyDefinition ParameterId { get; set; }
        public PropertyDefinition UnitId { get; set; }
        public string CommentPrefix { get; set; }
        public PropertyDefinition ReadingType { get; set; }
        public PropertyDefinition Comments { get; set; }
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
        // Default property is Inspection.InspectionType (enum)
        public PropertyDefinition Comments { get; set; }
        public PropertyDefinition SubLocation { get; set; }
        public PropertyDefinition MeasurementDeviceManufacturer { get; set; }
        public PropertyDefinition MeasurementDeviceModel { get; set; }
        public PropertyDefinition MeasurementDeviceSerialNumber { get; set; }
    }

    public class CalibrationDefinition : ActivityDefinition
    {
        // Default property is Calibration.Value
        public PropertyDefinition ParameterId { get; set; }
        public PropertyDefinition UnitId { get; set; }
        public PropertyDefinition Comments { get; set; }
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

    public class ControlConditionColumnDefinition : ActivityDefinition
    {
        // Default property is ControlCondition.ConditionType (picklist)
        public PropertyDefinition UnitId { get; set; }
        public PropertyDefinition Comments { get; set; }
        public PropertyDefinition Party { get; set; }
        public PropertyDefinition ControlCleanedType { get; set; }
        public PropertyDefinition ControlCode { get; set; }
        public PropertyDefinition DistanceToGage { get; set; }
    }

    public abstract class DischargeActivityDefinition : TimeRangeActivityDefinition
    {
        // Default property is DischargeActivity.TotalDischarge.Value
        public PropertyDefinition ChannelName { get; set; }
        public PropertyDefinition Comments { get; set; }
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
        // Default property is GageHeight.Value
        public PropertyDefinition Include { get; set; }
    }

    public class AdcpDischargeDefinition : DischargeActivityDefinition
    {
        // Default property is AdcpDischargeSection.TotalDischarge.Value
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
        // Default property is ManualGaugingDischargeSection.TotalDischarge.Value
        public PropertyDefinition SectionDischarge { get; set; }
        public PropertyDefinition StartPoint { get; set; }
        public PropertyDefinition DischargeMethod { get; set; }
        public PropertyDefinition VelocityAverageValue { get; set; }
        public PropertyDefinition VelocityObservationMethod { get; set; }
        public PropertyDefinition DeploymentMethod { get; set; }
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
        // Default property is MeterCalibrationEquation.Slope
        public PropertyDefinition RangeStart { get; set; }
        public PropertyDefinition RangeEnd { get; set; }
        public PropertyDefinition Intercept { get; set; }
        public PropertyDefinition InterceptUnitId { get; set; }
    }

    public class LevelSurveyDefinition : CoreDefinition
    {
        // Default property is OriginReferencePointName
        public PropertyDefinition Comments { get; set; }
        public PropertyDefinition Party { get; set; }
        public PropertyDefinition Method { get; set; }

        public LevelSurveyMeasurementDefinition LevelSurveyMeasurement { get; set; }
        public List<LevelSurveyMeasurementDefinition> LevelSurveyMeasurements { get; set; } = new List<LevelSurveyMeasurementDefinition>();
        public List<LevelSurveyMeasurementDefinition> AllLevelSurveyMeasurements => AllDefinitions(LevelSurveyMeasurement, LevelSurveyMeasurements);
    }

    public class LevelSurveyMeasurementDefinition : ActivityDefinition
    {
        // Default property is MeasuredElevation
        public PropertyDefinition ReferencePointName { get; set; }
        public PropertyDefinition Comments { get; set; }
    }
}
