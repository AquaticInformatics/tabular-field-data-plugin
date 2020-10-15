using System.Collections.Generic;

namespace BlazorTestDrive
{
    public class Examples
    {
        public static List<Example> AllExamples { get; } = new List<Example>
        {
            new Example
            {
                Id = "AirTemperature",
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
LOC1, 2020-Jan-12 12:35, 20.5
LOC2, 1988-Aug-8 15:10, -3.5"
            },
            new Example
            {
                Id = "French",
                Name = "Parse French dates and numbers",
                Description = "Uses the .NET French locale to parse dates and numbers.",
                ConfigText = @"
LocaleName = 'fr-FR' # Use the French locale
Location = '@Le Site'
Time = '@La Date'
Separator =';' # Since the comma is used a decimal point

[Reading]
Value = '@La Température'
ParameterId = 'TA'
UnitId = 'degC'",
                CsvText = @"
Le Site; La Date; La Température
LOC1; 2020-Janvier-11 12:35; 18,5
LOC2; 1988-Août-9 15:10; -4,5",
            },
            new Example
            {
                Id = "Spanish",
                Name = "Parse Spanish dates and numbers",
                Description = "Uses the .NET Spanish locale to parse dates and numbers.",
                ConfigText = @"
LocaleName = 'es-ES' # Use the Spanish locale to parse dates and numbers
Location = '@Sitio'
Time = '@Fecha'
Separator =';' # Since the comma is used a decimal point

[Reading]
Value = '@Temperatura'
ParameterId = 'TA'
UnitId = 'degC'",
                CsvText = @"
Sitio; Fecha; Temperatura
LOC1; 2020-Ene.-12 12:35; 20,5
LOC2; 1988-Ago.-8 15:10; -3,5",
            },
            new Example
            {
                Id = "ReadingsWithGrades",
                Name = "Readings with grades and qualifiers",
                Description = "This example shows how to bring in more than one qualifier per reading, using a semi-colon to separate qualifiers in your CSV data column.",
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
                Id = "ReadingsWithDatumConversion",
                Name = "Readings with datum conversion",
                Description = "This example shows how to bring in some Depth readings with optional datum conversion context, along with other reading types.",
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
                Id = "StageDischargeReadingsFormat",
                Name = "The StageDischargeReadings plugin format",
                Description = @"
This configuration emulates the now-obsolete
<a href=""https://github.com/AquaticInformatics/stage-discharge-readings-field-data-plugin#stage-discharge-readings-field-data-plugin"">StageDischargeReadings plugin</a>.
Note that it also includes a sparse list of <a href=""https://github.com/AquaticInformatics/tabular-field-data-plugin/wiki/GageHeightMeasurements""<code>[[PanelDischargeSummary.GageHeightMeasurements]]</code></a> sections.",
                ConfigText = @"
# A Tabular CSV configuration that exactly matches the older StageDischargeReadings format
CommentLinePrefix = '#'
Location = '@LocationIdentifier'

[Visit]
Party = '@Party'

[Reading]
Time = '@MeasurementStartDateTime | DateTimeOffset'
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
StartTime = '@MeasurementStartDateTime | DateTimeOffset'
EndTime = '@MeasurementEndDateTime | DateTimeOffset'
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
            new Example
            {
                Id = "Aliases",
                Name = "Use {Aliases} to transform your data",
                Description = "Aliases are a power feature for transforming invalid data from your CSV file into a form required by AQUARIUS.",
                ConfigText = @"
# Append the {aliasName} pattern to the end of a short form string
Location = '@Location{Locations}'
Time = '@Time'

[Reading]
Value = '@Value'
ParameterId = 'TA'
UnitId = '@Unit{Units}' 

[Aliases.Units]
'°C' = 'degC'
'°F' = 'degF'

[Aliases.Locations]
'Loc1' = 'LOCATION1'
'Loc2' = 'GWX0005J'",
                CsvText = @"
Location, Time, Value, Unit
Loc1, 2020-Apr-1, 0, °C
Loc2, 2020-Apr-1, 32, °F",
            },
            new Example
            {
                Id = "Survey123Example",
                Name = "Read exports from a Survey123 form",
                Description = @"
Parses readings collected from a custom Survey123 survey.
Uses aliases to work around old stations that have been renamed in AQUARIUS.",
                ConfigText = @"
Location = '@Please type in the site name:{Locations}'

[Visit]
Comment = '@Comments:'
Time = '@Visit Time: | Time Only | H:m'
MergeWithComment = '@Maintenance Issues:'

[[Visit.Times]]
ColumnHeader = 'Visit Date:'
Type = 'DateAndSurvey123Offset'
Format = 'M/d/yyyy h:m:s tt'

[[Readings]]
ParameterId = 'HG'
UnitId = 'ft'
Value = '@Staff Gage Reading 1 (ft):'
MergeWithComment = '#1 Staff Gage'

[[Readings]]
ParameterId = 'HG'
UnitId = 'ft'
Value = '@Staff Gage Reading 2 (ft):'
MergeWithComment = '#2 Staff Gage'

[[Readings]]
ParameterId = 'QR'
UnitId = 'ft^3/s'
Value = '@Flow Meter Reading 1 (cfs):'
MergeWithComment = '#1 Flow Meter'

[[Readings]]
ParameterId = 'QR'
UnitId = 'ft^3/s'
Value = '@Flow Meter Reading 2 (cfs):'
MergeWithComment = '#2 Flow Meter'

[[Readings]]
ParameterId = 'QR'
UnitId = 'ft^3/s'
Value = '@Discharge Measured in CFS'
MergeWithComment = 'Measured discharge'

[Aliases.Locations]
'Aberdeen_Waste' = 'Aberdeen Waste'
'SNK_RVR_ID' = 'SNK RVR ID'
'Sterling_Waste' = 'Sterling Waste'
",
                CsvText = @"
ObjectID,GlobalID,ESPA Monitoring Survey for:,Please type in the site name:,Visit Date:,Examiner:,Visit Time:,Station Date:,Station Time:,FIELD OBSERVATIONS,Type of Measurement:,Staff Gage Reading 1 (ft):,Logger Staff Reading 1 (ft):,Staff Gage Reading 2 (ft):,Logger Staff Reading 2 (ft):,Flow Meter Reading 1 (cfs):,Logger Meter Reading 1 (cfs):,Flow Meter Reading 2 (cfs):,Logger Meter Reading 2 (cfs):,Battery Voltage (V):,Internal Battery Voltage (V):,Discharge Measured?,Discharge Measured in CFS,Comments:,Maintenance Issues:,Do you need to follow up?,CreationDate,Creator,EditDate,Editor,x,y
1,881135cc-bc16-4f24-b2e3-5684b9e5952b,Pristine Springs,City of Twin,4/11/2019 6:00:00 AM,Michelle_Richman,07:17,4/11/2019 6:00:00 AM,07:17,,FlowMeter,,,,,0,0,0,0,13.3,3.36,No,,pumps were on the off cycle,,no,4/11/2019 1:22:44 PM,tsanabria_IDWR,4/11/2019 1:22:44 PM,tsanabria_IDWR,-114.477417,42.6029380003
2,44b2c672-eaa3-4d79-8226-5bbceaec19b1,Pristine Springs,Blue Lakes Weir,4/11/2019 6:00:00 AM,Michelle_Richman,07:41,4/11/2019 6:00:00 AM,07:41,,StaffGage,1.21,1.1898,,,,,,,12.67,3.29,No,,set gage to 1.21,,no,4/11/2019 1:47:33 PM,tsanabria_IDWR,4/11/2019 1:47:33 PM,tsanabria_IDWR,-114.468996999,42.6148190004
3,922dd702-7e68-43e9-9d57-62df2ff3c25b,Pristine,Blue Lakes Weir,6/12/2018 6:00:00 AM,Michelle_Richman,18:04,6/20/2018 6:00:00 AM,18:04,,StaffGage,1.14,1.175,,,,,,,13.43,3.51,No,,WL reset,,no,4/26/2019 7:35:12 PM,mrichman_IDWR,4/26/2019 7:35:12 PM,mrichman_IDWR,-114.468996999,42.6148190004
4,7860b540-702d-48b1-90d1-655a95cc5661,Pristine,City of Twin,6/21/2018 6:00:00 AM,Tito_Sanabria,07:23,6/21/2018 6:00:00 AM,07:23,,FlowMeter,,,,,15.24,19.29,15.53,19.78,13.23,3.38,No,,,,no,4/26/2019 7:38:42 PM,mrichman_IDWR,4/26/2019 7:38:42 PM,mrichman_IDWR,-114.477417,42.6029380003"
            },
            new Example
            {
                Id = "OttMFPro",
                Name = "OTT MF Pro *.TSV files",
                Description = "Uses plenty of regular expressions to pull out data from an OTT MF Pro discharge summary file.",
                DefaultLocation = "SomeLoc",
                ConfigText = @"
# This is our hint that it's an MF Pro file.
PrefaceMustContain = 'Model: MF pro'

#Location = '?' # There is no location information in an MF Pro file, so you'll need to provide a location context elsewhere.

# The next two settings tell the plugin that everything comes from the preface.
MaximumPrefaceLines = 0
NoDataRowsExpected = true

# The multi-line option must be enabled via the 'm' after the trailing backslash, to match across a newline.
# This is because the line FOLLOWING the 'Operator Name: xxxx' line is the only line with a timestamp we can parse.
# Yeah, it's weird. But yay, it works! Regex FTW!!
Time = '/^Operator Name: [^\n\r]+[\r\n]+(?<value>[^\r\n]+)/m'

[PanelDischargeSummary]
MeterType = 'Adv'
MeterCalibrationModel = 'MF Pro'
MeterCalibrationManufacturer = 'OTT'

# Parse the other measurement values using regular expressions
TotalDischarge  = '/^Total Discharge: (?<value>\S+) \S+$/'
DischargeUnitId = '/^Total Discharge: \S+ (?<value>\S+)$/{Units}'
DistanceUnitId  = '/^Stream Width: \S+ (?<value>\S+)$/{Units}'
WidthValue = '/^Stream Width: (?<value>\S+) \S+$/'
AreaValue   = '/^Total Area: (?<value>\S+) \S+$/'
AreaUnitId  = '/^Total Area: \S+ (?<value>\S+)$/{Units}'
MeanIndexVelocity = '/^Mean Velocity: (?<value>\S+) \S+$/'
VelocityUnitId = '/^Mean Velocity: \S+ (?<value>\S+)$/{Units}'
NumberOfVerticals = '/^# of Stations: (?<value>.+)$/'
MeterCalibrationSerialNumber = '/^s\/n: (?<value>.+)$/'
MeterCalibrationFirmwareVersion = '/^Boot: (?<value>.+)$/'
MeterCalibrationSoftwareVersion = '/^Application: (?<value>.+)$/'

[Aliases.Units]
'lps' = 'l/s'

[Aliases.StartPoint]
'Right edge water' = 'RightEdgeOfWater'
'Left edge water' = 'LeftEdgeOfWater'

[Aliases.DischargeMethod]
'Mid-section' = 'MidSection'
'Mean-section' = 'MeanSection'
",
                CsvText = @"
Profile Name: 21000110
Operator Name: KIJEN
10:59:22  04.16.2020

Model: MF pro
s/n: 000000337289
Boot: v1.00
Application: v2.00

Sensor Type: Velocity and Depth
s/n: 170110338107
Boot: v1.00
Application: v1.02

Filter: FPA  Parameter: 30 s
Pre-filter: On  Rank: 5
EMI: 50Hz.

Station Entry: Non-fixed
Flow Calculation: Mid-section
Start Edge: Right edge water
# of Stations: 16
Stream Width: 0.920 m
Total Discharge: 28.76 lps
Total Area: 0.164 m^2
Mean Depth: 0.178 m
Mean Velocity: 0.175 m/s

Measurement Results:
Time	Station	Location (m)	Method	Method	Depth (m)	Ice thickness (m)	Stage Reference (m)	Edge Factor	Correction factor	Surface (m/s)	Surface (m):	0.2 (m/s)	0.2 (m):	0.4 (m/s)	0.4 (m):	0.5 (m/s)	0.5 (m):	0.6 (m/s)	0.6 (m):	0.62 (m/s)	0.62 (m):	0.7 (m):	0.7 (m/s)	0.8 (m/s)	0.8 (m):	0.9 (m/s)	0.9 (m):	Bed (m/s)	Bed (m):	Average Velocity (m/s)	Area (m^2)	Flow (lps)	
10:15:12	1	4.360	Right edge water	0	0.215	-	0.679	0.700	-	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.001	0.063	
10:19:17	2	4.370	3 point	3	0.215	-	0.679	-	-	0.000	0.000	0.096	0.040	0.000	0.000	0.000	0.000	0.101	0.125	0.000	0.000	0.000	0.000	0.039	0.170	0.000	0.000	0.000	0.000	0.084	0.010	0.814	
10:21:46	3	4.450	3 point	3	0.208	-	0.679	-	-	0.000	0.000	0.184	0.041	0.000	0.000	0.000	0.000	0.165	0.125	0.000	0.000	0.000	0.000	0.122	0.166	0.000	0.000	0.000	0.000	0.159	0.013	2.147	
10:48:51	4	4.500	3 point	3	0.189	-	0.679	-	-	0.000	0.000	0.188	0.039	0.000	0.000	0.000	0.000	0.184	0.114	0.000	0.000	0.000	0.000	0.150	0.151	0.000	0.000	0.000	0.000	0.177	0.009	1.674	
10:26:48	5	4.550	3 point	3	0.187	-	0.679	-	-	0.000	0.000	0.215	0.036	0.000	0.000	0.000	0.000	0.213	0.113	0.000	0.000	0.000	0.000	0.169	0.150	0.000	0.000	0.000	0.000	0.202	0.009	1.895	
10:51:45	6	4.600	3 point	3	0.180	-	0.679	-	-	0.000	0.000	0.227	0.037	0.000	0.000	0.000	0.000	0.215	0.108	0.000	0.000	0.000	0.000	0.172	0.145	0.000	0.000	0.000	0.000	0.207	0.009	1.869	
10:32:13	7	4.650	3 point	3	0.183	-	0.679	-	-	0.000	0.000	0.229	0.037	0.000	0.000	0.000	0.000	0.194	0.112	0.000	0.000	0.000	0.000	0.170	0.147	0.000	0.000	0.000	0.000	0.197	0.014	2.702	
10:35:01	8	4.750	3 point	3	0.159	-	0.679	-	-	0.000	0.000	0.210	0.032	0.000	0.000	0.000	0.000	0.182	0.095	0.000	0.000	0.000	0.000	0.160	0.127	0.000	0.000	0.000	0.000	0.183	0.013	2.485	
10:37:37	9	4.820	3 point	3	0.156	-	0.679	-	-	0.000	0.000	0.237	0.032	0.000	0.000	0.000	0.000	0.207	0.093	0.000	0.000	0.000	0.000	0.182	0.125	0.000	0.000	0.000	0.000	0.208	0.012	2.445	
10:55:06	10	4.900	3 point	3	0.162	-	0.679	-	-	0.000	0.000	0.253	0.034	0.000	0.000	0.000	0.000	0.221	0.096	0.000	0.000	0.000	0.000	0.160	0.130	0.000	0.000	0.000	0.000	0.214	0.010	2.249	
10:43:04	11	4.950	3 point	3	0.161	-	0.679	-	-	0.000	0.000	0.250	0.032	0.000	0.000	0.000	0.000	0.215	0.097	0.000	0.000	0.000	0.000	0.189	0.130	0.000	0.000	0.000	0.000	0.217	0.012	2.631	
10:45:47	12	5.050	3 point	3	0.169	-	0.679	-	-	0.000	0.000	0.240	0.034	0.000	0.000	0.000	0.000	0.177	0.102	0.000	0.000	0.000	0.000	0.137	0.135	0.000	0.000	0.000	0.000	0.183	0.013	2.316	
10:58:14	13	5.100	3 point	3	0.182	-	0.679	-	-	0.000	0.000	0.223	0.038	0.000	0.000	0.000	0.000	0.173	0.108	0.000	0.000	0.000	0.000	0.137	0.146	0.000	0.000	0.000	0.000	0.176	0.009	1.608	
00:00:00	14	5.150	3 point	3	0.183	-	0.679	-	-	0.000	0.000	0.235	0.039	0.000	0.000	0.000	0.000	0.184	0.110	0.000	0.000	0.000	0.000	0.144	0.148	0.000	0.000	0.000	0.000	0.186	0.014	2.567	
00:00:00	15	5.250	3 point	3	0.181	-	0.679	-	-	0.000	0.000	0.108	0.036	0.000	0.000	0.000	0.000	0.087	0.109	0.000	0.000	0.000	0.000	0.095	0.145	0.000	0.000	0.000	0.000	0.094	0.012	1.106	
10:59:02	16	5.280	Left edge water	0	0.192	-	0.679	0.700	-	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.000	0.003	0.190	
",
            }
        };
    }

    public class Example
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string DefaultLocation { get; set; }
        public string ConfigText { get; set; }
        public string CsvText { get; set; }
    }
}
