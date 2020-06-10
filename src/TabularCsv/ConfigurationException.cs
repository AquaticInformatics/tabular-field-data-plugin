using System;

namespace TabularCsv
{
    public class ConfigurationException : Exception
    {
        public ConfigurationException(string message)
        : base(message)
        {
        }
    }
}
