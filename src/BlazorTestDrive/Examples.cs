using System.Collections.Generic;
using System.Linq;

namespace BlazorTestDrive
{
    public class Examples
    {
        public static List<Example> AllExamples { get; } = new List<Example>
        {
            new Example
            {
                Name = "Air Temperature readings",
                ConfigText = @"
Location = '@The Location'
Time = '@The Time'

[Reading]
Value = '@The Temperature'
ParameterId = 'TA'
UnitId = 'degC'",
                CsvText = @"
The Location, The Time, The Temperature
LOC1, 2020-Jun-12 12:35, 20.5
LOC2, 1988-Feb-8 15:10, -3.5"
            },
            new Example
            {
                Name = "Readings with grades and qualifiers",
                ConfigText = @"
Location = '@Location'
Time = '@Time'

[Reading]
ParameterId = '@Parameter'
UnitId = '@Unit'
Value = '@Value'
ReadingType = '@ReadingType'
GradeName = '@Grade'

# Note reading qualifiers aren't the same a time-series qualifiers.
# Use the System Config page to configured the 'Reading Qualifier Type' drop-down list.
ReadingQualifiers = '@Qualifiers'
ReadingQualifierSeparators = ';'",
                CsvText =
                    @"
Location, Time, Parameter, Value, Unit, ReadingType, SubLocation, Grade, Qualifiers
LOC1, 2020-04-01 8:15, TA, 12.5, degC, Routine, , GOOD
LOC2, 2020-04-01 12:35, DepthToWater, 0.8, m, Routine, Pipe 1, FAIR, TIDAL;WET
LOC3, 2020-04-01 23:42, DepthToWater, 0.5, m, Routine, Pipe 1, GOOD"
            },
            new Example
            {
                Name = "Readings with datum conversion",
                ConfigText = @"
Location = '@Location'
Time = '@Time'

[Reading]
ParameterId = '@Parameter'
UnitId = '@Unit'
Value = '@Value'
ReadingType = '@ReadingType'
SubLocation = '@SubLocation'
Comment = '@Comments'
ReferencePointName = '@ReferencePoint'
UseLocationDatumAsReference = '@UseLocalAssumedDatum'",
                CsvText = @"
Location, Time, Parameter, Value, Unit, ReadingType, SubLocation, Comments, ReferencePoint, UseLocalAssumedDatum
LOC1, 2020-04-01 8:15, TA, 12.5, degC, Routine, , Manual air temp reading
LOC2, 2020-04-01 12:35, DepthToWater, 0.8, m, Routine, Pipe 1, Top of casing verification, MeasuringPoint
LOC3, 2020-04-01 23:42, DepthToWater, 0.5, m, Routine, Pipe 1, From ground surface, , true",
            },
            new Example
            {
                Name = "The StageDischargeReadings plugin format",
                ConfigText = @"
# A Tabular CSV configuration that exactly matches the older StageDischargeReadings format
CommentLinePrefix = '#'
Location = '@LocationIdentifier'

[Visit]
Party = '@Party'

[Reading]
Time = '@MeasurementStartDateTime'
Comment = '@Comments'
ParameterId = '@ReadingParameter'
UnitId = '@ReadingUnits'
Value = '@ReadingValue'
Method = '@ReadingMethod'
ReadingType = '@ReadingType'
Publish = '@ReadingPublish'
Uncertainty = '@ReadingUncertainty'
MeasurementDeviceManufacturer = '@ReadingDeviceManufacturer'
MeasurementDeviceModel = '@ReadingDeviceModel'
MeasurementDeviceSerialNumber = '@ReadingDeviceSerialNumber'
SubLocation = '@ReadingSublocation'

[PanelDischargeSummary]
Party = '@Party'
Comment = '@Comments'
StartTime = '@MeasurementStartDateTime'
EndTime = '@MeasurementEndDateTime'
MeasurementId = '@MeasurementId'
DistanceUnitId = '@WidthUnits'
TotalDischarge = '@Discharge'
DischargeUnitId  = '@DischargeUnits'
ChannelName = '@ChannelName'
WidthValue = '@ChannelWidth'
AreaValue = '@ChannelArea'
AreaUnitId = '@AreaUnits'
MeanIndexVelocity = '@ChannelVelocity'
VelocityUnitId = '@VelocityUnits'

[[PanelDischargeSummary.GageHeightMeasurements]]
Value = '@StageAtStart'

[[PanelDischargeSummary.GageHeightMeasurements]]
Value = '@StageAtEnd'
",
                CsvText = @"
# A comment line
#
# AQUARIUS Stage-Discharge CSV v1.0
#
LocationIdentifier, MeasurementId, MeasurementStartDateTime,          MeasurementEndDateTime,            StageAtStart, StageAtEnd, StageUnits, Discharge, DischargeUnits, ChannelName, ChannelWidth, WidthUnits, ChannelArea, AreaUnits, ChannelVelocity, VelocityUnits, Party, Comments, ReadingParameter, ReadingUnits, ReadingValue, ReadingType, ReadingMethod, ReadingPublish, ReadingUncertainty, ReadingDeviceManufacturer, ReadingDeviceModel, ReadingDeviceSerialNumber, ReadingSublocation
LocationA         , 46791        , 2016-04-01T00:00:00.0000000Z,      2016-04-01T02:00:00.0000000Z,      12.0,         12.5,       ft,         32.3,      ft^3/s,         Main,        ,             ft,         ,            ft^2,      ,                ft/s,          Brian, ,         TW,                degC,          18,         Routine,     ,              true,           14.5,               man,                       mode,               ser,                        sub
LocationA         ,              , 2016-04-01T01:15:00.0000000Z,      2016-04-01T01:15:00.0000000Z,          ,             ,         ,             ,            ,             ,        ,               ,         ,                ,      ,                    ,          Dave,  Hot!,         TA,                degC,          44
                                                                                                    
LocationB         ,              , 2017-05-01T03:00:00.0000000+04:00, 2017-05-01T04:00:00.0000000+04:00, 8.7,          8.6,        ft,         13.5,      ft^3/s,         Main,        ,             ft,         ,            ft^2,      ,                ft/s,          ,      ""Bubbler hose was disturbed, so we remeasured""
LocationB         , 852345       , 2017-05-01T05:00:00.0000000+04:00, 2017-05-01T06:00:00.0000000+04:00, 9.4,          9.4,        ft,         13.6,      ft^3/s,         Main,        ,             ft,         ,            ft^2,      ,                ft/s


LocationA         , 46792        , 2016-04-02T03:00:00.0000000Z,      2016-04-02T04:00:00.0000000Z,      11.85,        12.7,       ft,        132.3,      ft^3/s,         Main,        23.4,         ft,         125.63,      ft^2,       85.2,           ft/s,          Doug , My oh my what a comment

LocationA         , 46793        , 2016-04-03T04:00:00.0000000Z,      2016-04-03T05:00:00.0000000Z,      ,             ,           ,          143.3,      ft^3/s,         Main,        23.4,         ft,         125.63,      ft^2,       85.2,           ft/s,          ,      A discharge without a stage measurement
"
            },
        };
    }

    public class Example
    {
        public string Name { get; set; }
        public string ConfigText { get; set; }
        public string CsvText { get; set; }
    }
}
