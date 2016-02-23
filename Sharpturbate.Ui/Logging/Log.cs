using NLog;

namespace Sharpturbate.Ui.Logging
{
    public static class Log
    {
        public static Logger Instance { get; private set; } = LogManager.GetCurrentClassLogger();
    }
}
