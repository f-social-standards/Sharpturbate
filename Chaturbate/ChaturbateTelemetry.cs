using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telemetry.Models;

namespace ChaturbateSharp
{
    public enum ChaturbateEventType
    {
        DownloadStart,
        DownloadStop,
        DownloadJoin,
        DownloadFinished,
        ParseURL,
        ParseStreams
    }
    public static class ChaturbateTelemetry
    {
        public static TelemetryDetails Config()
        {
            TelemetryDetails details = new TelemetryDetails();
            details.ApplicationName = "Chaturbate";
            details.Date = DateTime.Now;

            return details;
        }
    }
}
