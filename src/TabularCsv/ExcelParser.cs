using System;
using System.Data;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using ExcelDataReader;
using ExcelDataReader.Exceptions;

namespace TabularCsv
{
    public class ExcelParser
    {
        public bool TryLoadSingleSheet(Configuration configuration, byte[] fileBytes, out byte[] csvBytes)
        {
            csvBytes = null;

            if (!IsExcelFile(fileBytes))
                return false;

            try
            {
                using (var stream = new MemoryStream(fileBytes))
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    var dataSetConfig = new ExcelDataSetConfiguration
                    {
                        FilterSheet = (excelReader, sheetIndex) => configuration.SheetNumber.HasValue
                            ? sheetIndex == configuration.SheetNumber.Value - 1
                            : string.IsNullOrEmpty(configuration.SheetName) ||
                              configuration.SheetName.Equals(excelReader.Name,
                                  StringComparison.InvariantCultureIgnoreCase),
                        ConfigureDataTable = tableReader =>
                            new ExcelDataTableConfiguration
                            {
                                UseHeaderRow = false,
                            }
                    };

                    var dataSet = reader.AsDataSet(dataSetConfig);

                    var dataTable = dataSet
                        .Tables
                        .Cast<DataTable>()
                        .FirstOrDefault();

                    if (dataTable == null)
                        return false;

                    using (var memoryStream = new MemoryStream())
                    using (var writer = new StreamWriter(memoryStream, Encoding.UTF8))
                    {
                        ExportTable(writer, dataTable);

                        writer.Flush();
                        csvBytes = memoryStream.ToArray();
                        return true;
                    }
                }
            }
            catch (HeaderException)
            {
                return false;
            }
        }

        private bool IsExcelFile(byte[] fileBytes)
        {
            if (StartsWithMagicBytes(XlsMagicBytes, fileBytes))
                return true;

            if (!StartsWithMagicBytes(XlsxMagicBytes, fileBytes))
                return false;

            try
            {
                using (var stream = new MemoryStream(fileBytes))
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    var openDocRoot = archive.GetEntry("[Content_Types].xml");

                    // TODO: Could try to dig deeper than "Accept OpenOfficeDoc", but this should be quick enough for not
                    return openDocRoot != null;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool StartsWithMagicBytes(byte[] magicBytes, byte[] fileBytes)
        {
            return magicBytes.SequenceEqual(fileBytes.Take(magicBytes.Length));
        }

        private static readonly byte[] XlsxMagicBytes = { 0x50, 0x4b, 0x03, 0x04 }; // PKZIP .xlsx wrapper
        private static readonly byte[] XlsMagicBytes = { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }; // Older .xls format

        private void ExportTable(StreamWriter writer, DataTable table)
        {
            foreach (var row in table.Rows.Cast<DataRow>())
            {
                var columns = row
                    .ItemArray
                    .Select((c, i) => row.IsNull(i) ? string.Empty : CsvEscapedColumn(FormatCell(c)))
                    .ToList();

                for (var i = columns.Count - 1; i > 0; --i)
                {
                    if (!string.IsNullOrWhiteSpace(columns[i]))
                        break;

                    columns.RemoveAt(i);
                }

                writer.WriteLine(string.Join(", ", columns));
            }
        }

        private string FormatCell(object cell)
        {
            if (cell is DateTime dateTime)
                return dateTime.ToString("O");

            return $"{cell}";
        }

        private static string CsvEscapedColumn(string text)
        {
            return !CharactersRequiringEscaping.Any(text.Contains)
                ? text
                : $"\"{text.Replace("\"", "\"\"")}\"";
        }

        private static readonly char[] CharactersRequiringEscaping =
        {
            ',',
            '"',
            '\n',
            '\r'
        };
    }
}
