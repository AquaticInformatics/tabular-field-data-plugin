using System;
using System.Collections.Generic;
using System.Linq;

namespace TabularCsv
{
    public class PrefaceEndDetector
    {
        public string Separator { get; set; }
        private Configuration Configuration { get; }
        private List<string> ExpectedColumnHeaders { get; } = new List<string>();

        public PrefaceEndDetector(Configuration configuration)
        {
            Configuration = configuration;

            if (string.IsNullOrEmpty(Configuration.PrefaceEndsWith)
                && string.IsNullOrEmpty(Configuration.PrefaceEndsBefore)
                && Configuration.PrefaceRowCount == 0)
            {
                ExpectedColumnHeaders = Configuration
                    .GetColumnDefinitions()
                    .Where(c => c.HasNamedColumn)
                    .Select(c => c.ColumnHeader)
                    .Distinct()
                    .ToList();
            }
        }

        public bool IsFirstHeaderLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return false;

            if (!string.IsNullOrEmpty(Configuration.PrefaceEndsBefore) && line.StartsWith(Configuration.PrefaceEndsBefore, StringComparison.InvariantCultureIgnoreCase))
                return true;

            if (ExpectedColumnHeaders.Any())
            {
                var columns = line
                    .Split(Separator.ToCharArray())
                    .Select(s => s.Trim());

                return ExpectedColumnHeaders.All(headerName => columns.Contains(headerName));
            }

            return false;
        }

        public bool IsLastPrefaceLine(string line)
        {
            return !string.IsNullOrEmpty(Configuration.PrefaceEndsWith) && line.StartsWith(Configuration.PrefaceEndsWith, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
