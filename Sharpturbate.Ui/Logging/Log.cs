using Newtonsoft.Json;
using NLog;

namespace Sharpturbate.Ui.Logging
{
    public static class Log
    {
        private static Logger Instance { get; } = LogManager.GetCurrentClassLogger();

        private static readonly JsonSerializerSettings SerializationSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        public static void LogEvent(LogLevel level, object message)
        {
            Instance.Log(level, JsonConvert.SerializeObject(message, SerializationSettings));
        }
    }
}