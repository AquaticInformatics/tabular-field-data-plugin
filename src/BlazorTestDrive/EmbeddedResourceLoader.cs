using System.IO;
using System.Reflection;

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

        public static string LoadAsText(string path)
        {
            using (var stream = LoadEmbeddedResourceStream(path))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        public static byte[] LoadAsBytes(string path)
        {
            using (var stream = LoadEmbeddedResourceStream(path))
            using (var reader = new BinaryReader(stream))
            {
                return reader.ReadBytes((int)stream.Length);
            }
        }
    }
}
