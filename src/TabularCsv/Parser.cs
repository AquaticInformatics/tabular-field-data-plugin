using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FieldDataPluginFramework;
using FieldDataPluginFramework.Context;
using FieldDataPluginFramework.Results;
using NotVisualBasic.FileIO;

namespace TabularCsv
{
    public class Parser
    {
        public Parser(ILog logger, IFieldDataResultsAppender resultsAppender)
        {
            Log = logger;
            ResultsAppender = new DelayedAppender(resultsAppender);
        }

        private ILog Log { get; }
        private DelayedAppender ResultsAppender { get; }
        private LocationInfo LocationInfo { get; set; }

        private const string DefaultFieldSeparator = ",";

        public ParseFileResult Parse(Stream stream, LocationInfo locationInfo = null)
        {
            var csvText = ReadTextFromStream(stream);

            if (csvText == null)
                return ParseFileResult.CannotParse();

            try
            {
                LocationInfo = locationInfo;

                var configurations = LoadConfigurations();

                using (ResultsAppender)
                {
                    var result = ParseFileResult.CannotParse();

                    foreach (var configuration in configurations)
                    {
                        using (LocaleScope.WithLocale(configuration.LocaleName))
                        {
                            result = ParseDataFile(configuration, csvText);

                            if (result.Status == ParseFileStatus.CannotParse) continue;

                            return result;
                        }
                    }

                    return result;
                }
            }
            catch (Exception exception)
            {
                return ParseFileResult.SuccessfullyParsedButDataInvalid(exception);
            }
        }

        private ParseFileResult ParseDataFile(Configuration configuration, string csvText)
        {
            var rowParser = new RowParser
            {
                Log = Log,
                LocationInfo = LocationInfo,
                ResultsAppender = ResultsAppender,
                Configuration = configuration,
            };

            var (prefaceLines, dataRowReader) = ExtractPrefaceLines(configuration, csvText);

            rowParser.AddPrefaceLines(prefaceLines);

            if (!rowParser.IsPrefaceValid())
                return ParseFileResult.CannotParse();

            if (configuration.IsHeaderRowRequired)
            {
                rowParser.RemainingHeaderLines = configuration.HeaderRowCount > 0
                    ? configuration.HeaderRowCount
                    : 1;
            }

            var dataRowCount = 0;

            using (var reader = dataRowReader)
            {
                var fieldParser = GetCsvParser(reader, configuration.Separator ?? DefaultFieldSeparator);

                while(!fieldParser.EndOfData)
                {
                    rowParser.LineNumber = rowParser.PrefaceLineCount + fieldParser.LineNumber;

                    string[] fields = null;

                    try
                    {
                        fields = fieldParser.ReadFields();
                    }
                    catch (Exception)
                    {
                        if (dataRowCount == 0)
                        {
                            // We'll hit this when the plugin tries to parse a text file that is not CSV, like a JSON document.
                            return ParseFileResult.CannotParse();
                        }
                    }

                    if (fields == null)
                        continue;

                    if (fields.Length > 0 && !string.IsNullOrEmpty(configuration.CommentLinePrefix) && fields[0].StartsWith(configuration.CommentLinePrefix))
                        continue;

                    if (dataRowCount == 0 && configuration.IsHeaderRowRequired)
                    {
                        try
                        {
                            if (rowParser.IsHeaderFullyParsed(fields))
                                ++dataRowCount;

                            continue;
                        }
                        catch (Exception exception)
                        {
                            // Most CSV files have a header.
                            // So a problem mapping the header is a strong indicator that this CSV file is not intended for us.
                            if (exception is AllHeadersMissingException)
                            {
                                // When all headers are missing, then we should exit without logging anything special.
                                // We'll just let the other plugins have a turn
                            }
                            else
                            {
                                // Some of the headers matched, so log a warning before returning CannotParse.
                                // This might be the only hint in the log that the configuration is incorrect.
                                Log.Info($"Partial headers detected: {exception.Message}");
                            }

                            return ParseFileResult.CannotParse();
                        }
                    }

                    try
                    {
                        rowParser.Parse(fields);
                    }
                    catch (Exception exception)
                    {
                        if (!ResultsAppender.AnyResultsAppended) throw;

                        Log.Error($"Ignoring invalid CSV row: {exception.Message}");
                    }

                    ++dataRowCount;
                }

                if (dataRowCount == 0)
                {
                    if (configuration.NoDataRowsExpected)
                    {
                        // When all the data comes from the preface, then this will parse the preface context
                        rowParser.Parse(new string[0]);
                    }
                    else
                    {
                        // No rows were parsed.
                        if (rowParser.RemainingHeaderLines > 0)
                            return ParseFileResult.CannotParse("No matching header row was detected.");

                        return ParseFileResult.CannotParse();
                    }
                }

                return ParseFileResult.SuccessfullyParsedAndDataValid();
            }
        }

        private CsvTextFieldParser GetCsvParser(StringReader reader, string delimiter)
        {
            var rowParser = new CsvTextFieldParser(reader)
            {
                Delimiters = new[] {delimiter},
                TrimWhiteSpace = true,
                HasFieldsEnclosedInQuotes = true,
            };

            return rowParser;
        }

        private (IEnumerable<string> PrefaceLines, StringReader RowReader) ExtractPrefaceLines(Configuration configuration, string csvText)
        {
            var prefaceLines = new List<string>();

            if (!configuration.IsPrefaceExpected)
                return (prefaceLines, new StringReader(csvText));

            var prefaceEndDetector = new PrefaceEndDetector(configuration)
            {
                Separator = configuration.Separator ?? DefaultFieldSeparator
            };

            using (var reader = new StringReader(csvText))
            {
                while (true)
                {
                    var line = reader.ReadLine();

                    if (line == null)
                        break;

                    if (prefaceEndDetector.IsFirstHeaderLine(line))
                    {
                        // This line needs to be included in the start of the header section
                        var builder = new StringBuilder(line);
                        builder.AppendLine();
                        builder.Append(reader.ReadToEnd());

                        return (prefaceLines, new StringReader(builder.ToString()));
                    }

                    prefaceLines.Add(line);

                    if (configuration.MaximumPrefaceLines > 0 && prefaceLines.Count > configuration.MaximumPrefaceLines)
                        throw new Exception($"{nameof(configuration.MaximumPrefaceLines)} of {configuration.MaximumPrefaceLines} exceeded.");

                    if (configuration.PrefaceRowCount > 0 && prefaceLines.Count >= configuration.PrefaceRowCount)
                        break;

                    if (prefaceEndDetector.IsLastPrefaceLine(line))
                        break;
                }

                return (prefaceLines, new StringReader(reader.ReadToEnd()));
            }
        }

        private string ReadTextFromStream(Stream stream)
        {
            try
            {
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private List<Configuration> LoadConfigurations()
        {
            var pluginConfigurations = ResultsAppender.GetPluginConfigurations();

            if (!pluginConfigurations.Any())
            {
                Log.Error($"No configurations loaded.");
                return new List<Configuration>();
            }

            var configurationLoader = new ConfigurationLoader
            {
                Log = Log
            };

            var allConfigurations = pluginConfigurations
                .Select(kvp => configurationLoader.Load(kvp.Key, kvp.Value))
                .ToList();

            var configurations = allConfigurations
                .Where(configuration => !configuration?.IsDisabled ?? false)
                .OrderBy(configuration => configuration.Priority)
                .ToList();

            if (!configurations.Any())
            {
                Log.Error($"No enabled configurations found. ({pluginConfigurations.Count} were disabled or invalid).");
                return new List<Configuration>();
            }

            configurations = configurations
                .Where(configuration => LocationInfo != null || configuration.Location != null)
                .ToList();

            foreach (var configuration in configurations)
            {
                new ConfigurationValidator
                    {
                        LocationInfo = LocationInfo,
                        Configuration = configuration
                    }
                    .Validate();
            }

            return configurations;
        }
    }
}
