using System.Reflection;
using System.Text;

namespace BlazorTestDrive
{
    public static class EmbeddedResourceLoader
    {
        private static Stream LoadEmbeddedResourceStream(string path)
        {
            // ReSharper disable once PossibleNullReferenceException
            var resourceName = $"{MethodBase.GetCurrentMethod().DeclaringType.Namespace}.{path.Replace(Path.DirectorySeparatorChar, '.')}";

            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);

            if (stream == null)
                throw new FileNotFoundException($"Can't load '{resourceName}' as embedded resource.");

            return stream;
        }

        public static string LoadAsText(string path, Encoding? encoding = null)
        {
            using var stream = LoadEmbeddedResourceStream(path);
            using var reader = CreateTextReader(stream, encoding);

            return reader.ReadToEnd();
        }

        private static StreamReader CreateTextReader(Stream stream, Encoding? encoding)
        {
            if (encoding == null)
                return new StreamReader(stream);

            return new StreamReader(stream, encoding);
        }

        public static byte[] LoadAsBytes(string path)
        {
            using var stream = LoadEmbeddedResourceStream(path);
            using var reader = new BinaryReader(stream);

            return reader.ReadBytes((int)stream.Length);
        }
    }
}
