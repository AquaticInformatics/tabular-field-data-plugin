using System;

namespace TabularCsv
{
    public class AllHeadersMissingException : Exception
    {
        public AllHeadersMissingException(string message)
            : base(message)
        {
        }
    }
}
