using System;
using System.Globalization;
using System.Threading;

namespace TabularCsv
{
    public class LocaleScope : IDisposable
    {
        public static LocaleScope WithLocale(string localeName)
        {
            var cultureInfo = string.IsNullOrEmpty(localeName)
                ? CultureInfo.InvariantCulture
                : CultureInfo.GetCultureInfo(localeName);

            return new LocaleScope(cultureInfo);
        }

        private CultureInfo PreviousThreadCulture { get; set; }

        private LocaleScope(CultureInfo cultureInfo)
        {
            PreviousThreadCulture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = cultureInfo;
        }

        public void Dispose()
        {
            RestorePreviousCulture();
        }

        private void RestorePreviousCulture()
        {
            if (PreviousThreadCulture == null)
                return;

            Thread.CurrentThread.CurrentCulture = PreviousThreadCulture;
            PreviousThreadCulture = null;
        }
    }
}
