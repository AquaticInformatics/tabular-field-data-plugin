using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DocGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Environment.ExitCode = 1;

                if (args.Length < 1)
                    throw new ArgumentException($"usage: {nameof(DocGenerator)}.exe pathToConfiguration.cs");

                new Program()
                    .Run(args[0]);

                Environment.ExitCode = 0;
            }
            catch (Exception exception)
            {
                Console.WriteLine($"ERROR: {exception.Message}\n{exception.StackTrace}");
            }
        }

        private void Run(string path)
        {
            if (!File.Exists(path))
                throw new ArgumentException($"'{path}' does not exist.");

            Console.WriteLine($"classDiagram");
            Console.WriteLine($"");

            var className = string.Empty;

            void CloseClassDefinition()
            {
                if (string.IsNullOrEmpty(className))
                    return;

                Console.WriteLine($"  }}");
                Console.WriteLine($"  link {className} \"https://github.com/AquaticInformatics/tabular-field-data-plugin/wiki/{className}\" \"See {className} details\"");
                Console.WriteLine($"  ");
            }

            foreach (var line in File.ReadAllLines(path))
            {
                var match = ClassRegex.Match(line);

                if (match.Success)
                {
                    var baseClass = SanitizeClassName(match.Groups["baseClass"].Value);
                    var derivedClass = SanitizeClassName(match.Groups["derivedClass"].Value);

                    CloseClassDefinition();

                    className = derivedClass;

                    Console.WriteLine($"  {baseClass} <|-- {derivedClass}");
                    Console.WriteLine($"  class {derivedClass} {{");
                    continue;
                }

                match = PropertyRegex.Match(line);

                if (!match.Success)
                    continue;

                var propertyName = match.Groups["propertyName"].Value;

                Console.WriteLine($"    {propertyName}");
            }

            CloseClassDefinition();
        }

        private string SanitizeClassName(string name)
        {
            return name.Replace("Definition", string.Empty);
        }

        private static readonly Regex ClassRegex = new Regex(@"public\s+(abstract\s+)?class\s+(?<derivedClass>\w+)\s*:\s*(?<baseClass>\w+)\s*$");
        private static readonly Regex PropertyRegex = new Regex(@"public\s+.+\s+(?<propertyName>\w+)\s*\{\s*get;\s*set;\s*\}");
    }
}
