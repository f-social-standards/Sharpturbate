using System;
using Sharpturbate.Core.Aspects.Actions;
using Sharpturbate.Core.Telemetry.Enums;
using Sharpturbate.Core.Telemetry.Models;
using Telemetry.Net.Core;
using Telemetry.Net.DataModel;

namespace Sharpturbate.Core.Aspects.Parsers
{
    internal static class ErrorParser
    {
        internal static void ExceptionInfo(Exception e)
        {
            Safe.Run(async () =>
            {
                var data = new TelemetryData
                {
                    EventType = EventType.DownloadError,
                    EventData = new Error(e)
                };

                await TelemetryJs.LogAsync(data, true);
            });
        }
    }
}