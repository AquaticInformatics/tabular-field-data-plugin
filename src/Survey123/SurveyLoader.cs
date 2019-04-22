using System;
using System.Collections.Generic;
using System.IO;
using ServiceStack;

namespace Survey123
{
    public class SurveyLoader
    {
        public Survey Load(string path)
        {
            if (!File.Exists(path))
                throw new Exception($"'{path}' does not exist.");

            return File.ReadAllText(path).FromJson<Survey>();
        }

        public Survey CreateDefaultSurvey()
        {
            return new Survey
            {
                Name = "IDWR field data",
                FirstLineIsHeader = true,
                LocationColumn = new ColumnDefinition
                {
                    ColumnHeader = "Please type in the site name:"
                },
                CommentColumns = new List<MergingTextColumnDefinition>
                {
                    new MergingTextColumnDefinition
                    {
                        ColumnHeader = "Comments:",
                    },
                    new MergingTextColumnDefinition
                    {
                        ColumnHeader = "Maintenance Issues:",
                        Prefix = "Maintenance Issues: "
                    },
                },
                PartyColumns = new List<MergingTextColumnDefinition>
                {
                    new MergingTextColumnDefinition
                    {
                        ColumnHeader = "Examiner:"
                    },
                },
                TimestampColumns = new List<TimestampColumnDefinition>
                {
                    new TimestampColumnDefinition
                    {
                        ColumnHeader = "Visit Date:",
                        Format = "M/d/yyyy h:m:s tt",
                        Type = TimestampType.DateAndSurvey123Offset
                    },
                    new TimestampColumnDefinition
                    {
                        ColumnHeader = "Visit Time:",
                        Format = "H:m",
                        Type = TimestampType.TimeOnly
                    },
                },
                ReadingColumns = new List<ReadingColumnDefinition>
                {
                    new ReadingColumnDefinition
                    {
                        ColumnHeader = "Staff Gage Reading 1 (ft):",
                        CommentPrefix = "#1 Staff Gage",
                        ParameterId = "HG",
                        UnitId = "ft",
                    },
                    new ReadingColumnDefinition
                    {
                        ColumnHeader = "Staff Gage Reading 2 (ft):",
                        CommentPrefix = "#2 Staff Gage",
                        ParameterId = "HG",
                        UnitId = "ft",
                    },
                    new ReadingColumnDefinition
                    {
                        ColumnHeader = "Logger Staff Reading 1 (ft):",
                        CommentPrefix = "#1 Logger Staff",
                        ParameterId = "HG",
                        UnitId = "ft",
                    },
                    new ReadingColumnDefinition
                    {
                        ColumnHeader = "Logger Staff Reading 2 (ft):",
                        CommentPrefix = "#2 Logger Staff",
                        ParameterId = "HG",
                        UnitId = "ft",
                    },
                    new ReadingColumnDefinition
                    {
                        ColumnHeader = "Flow Meter Reading 1 (cfs):",
                        CommentPrefix = "#1 Flow Meter",
                        ParameterId = "QR",
                        UnitId = "ft^3/s",
                    },
                    new ReadingColumnDefinition
                    {
                        ColumnHeader = "Logger Gage Reading 1 (cfs):",
                        CommentPrefix = "#1 Logger Gage",
                        ParameterId = "QR",
                        UnitId = "ft^3/s",
                    },
                    new ReadingColumnDefinition
                    {
                        ColumnHeader = "Flow Meter Reading 2 (cfs):",
                        CommentPrefix = "#2 Flow Meter",
                        ParameterId = "QR",
                        UnitId = "ft^3/s",
                    },
                    new ReadingColumnDefinition
                    {
                        ColumnHeader = "Logger Gage Reading 2 (cfs):",
                        CommentPrefix = "#2 Logger Gage",
                        ParameterId = "QR",
                        UnitId = "ft^3/s",
                    },
                    new ReadingColumnDefinition
                    {
                        ColumnHeader = "Battery Voltage (V):",
                        CommentPrefix = "Battery Voltage",
                        ParameterId = "VB",
                        UnitId = "V",
                    },
                    new ReadingColumnDefinition
                    {
                        ColumnHeader = "Internal Battery Voltage (V):",
                        CommentPrefix = "Internal voltage",
                        ParameterId = "VB",
                        UnitId = "V",
                    },
                }
            };
        }
    }
}
