using System;

namespace Survey123
{
    public class AllHeadersMissingException : Exception
    {
        public AllHeadersMissingException(string message)
            : base(message)
        {
        }
    }
}
