using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DocGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Environment.ExitCode = 1;

                if (args.Length < 2)
                    throw new ArgumentException($"usage: {nameof(DocGenerator)}.exe pathToConfiguration.cs wikiOutputPath");

                new Program()
                    .Run(args[0], args[1]);

                Environment.ExitCode = 0;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"ERROR: {exception.Message}\n{exception.StackTrace}");
            }
        }

        private void Run(string sourceCodePath, string wikiOutputPath)
        {
            if (!File.Exists(sourceCodePath))
                throw new ArgumentException($"'{sourceCodePath}' does not exist.");

            if (!Directory.Exists(wikiOutputPath))
                throw new ArgumentException($"'{wikiOutputPath}' does not exist.");

            Console.WriteLine($"classDiagram");
            Console.WriteLine($"");

            var baseClass = string.Empty;
            var className = string.Empty;
            var propertyNames = new List<string>();

            var summaryBuilder = new StringBuilder();

            summaryBuilder
                .AppendLine($"# Configuration Summary")
                .AppendLine()
                .AppendLine($"The following definitions are supported:")
                .AppendLine()
                .AppendLine($"| Definition | Summary |")
                .AppendLine($"|---|---|");

            void CloseClassDefinition()
            {
                if (string.IsNullOrEmpty(className))
                    return;

                summaryBuilder
                    .AppendLine($"| [{className}](./{className}) | |");

                var builder = new StringBuilder();

                builder
                    .AppendLine($"# {className} Details")
                    .AppendLine()
                    .AppendLine($"The {className} definition derives from the [{baseClass}](./{baseClass}) definition, and includes all of the base class properties.")
                    .AppendLine()
                    .AppendLine($"| PropertyName | Required | Description |")
                    .AppendLine($"|---|---|---|");

                foreach (var propertyName in propertyNames)
                {
                    var required = propertyNames.IndexOf(propertyName) == 0
                        ? "Y"
                        : "N";

                    builder
                        .AppendLine($"| {propertyName} | {required} | |");
                }

                builder
                    .AppendLine();

                File.WriteAllText(Path.Combine(wikiOutputPath, $"{className}.md"), builder.ToString());

                Console.WriteLine($"  {baseClass} <|-- {className}");
                Console.WriteLine($"  class {className} {{");

                foreach (var propertyName in propertyNames)
                {
                    Console.WriteLine($"    {propertyName}");
                }

                Console.WriteLine($"  }}");
                Console.WriteLine($"  link {className} \"https://github.com/AquaticInformatics/tabular-field-data-plugin/wiki/{className}\" \"See {className} details\"");
                Console.WriteLine($"  ");

                propertyNames.Clear();
            }

            foreach (var line in File.ReadAllLines(sourceCodePath))
            {
                var match = ClassRegex.Match(line);

                if (match.Success)
                {
                    CloseClassDefinition();

                    baseClass = SanitizeClassName(match.Groups["baseClass"].Value);
                    className = SanitizeClassName(match.Groups["derivedClass"].Value);

                    continue;
                }

                match = PropertyRegex.Match(line);

                if (!match.Success)
                    continue;

                var propertyName = match.Groups["propertyName"].Value;

                propertyNames.Add(propertyName);
            }

            CloseClassDefinition();

            File.WriteAllText(Path.Combine(wikiOutputPath, $"Summary.md"), summaryBuilder.ToString());
        }

        private string SanitizeClassName(string name)
        {
            return name.Replace("Definition", string.Empty);
        }

        private static readonly Regex ClassRegex = new Regex(@"public\s+(abstract\s+)?class\s+(?<derivedClass>\w+)\s*:\s*(?<baseClass>\w+)\s*$");
        private static readonly Regex PropertyRegex = new Regex(@"public\s+.+\s+(?<propertyName>\w+)\s*\{\s*get;\s*set;\s*\}");
    }
}
