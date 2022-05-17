using System.Text;

namespace BlazorTestDrive
{
    public class Encodings
    {
        static Encodings()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public static List<EncodingInfo> AllEncodings => Encoding
            .GetEncodings()
            .OrderBy(e => e.Name, StringComparer.InvariantCultureIgnoreCase)
            .ToList();
    }
}
