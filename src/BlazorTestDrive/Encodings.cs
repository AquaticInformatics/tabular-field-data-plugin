using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BlazorTestDrive
{
    public class Encodings
    {
        public static List<EncodingInfo> AllEncodings { get; } = Encoding
            .GetEncodings()
            .OrderBy(e => e.Name, StringComparer.InvariantCultureIgnoreCase)
            .ToList();
    }
}
