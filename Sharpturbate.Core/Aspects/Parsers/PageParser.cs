using System;
using System.Linq;
using PostSharp.Aspects;
using Sharpturbate.Core.Telemetry.Enums;
using Sharpturbate.Core.Telemetry.Models;
using Telemetry.Net.Core;
using Telemetry.Net.DataModel;

namespace Sharpturbate.Core.Aspects.Parsers
{
    public static class PageParser
    {
        public const string MethodName = "GetStreamsAsync";

        public static async void PageInfo(MethodExecutionArgs args)
        {
            if (args.Method.Name != MethodName) return;

            var roomType = args.Arguments.FirstOrDefault()?.ToString();
            var roomPage = (int?) args.Arguments.LastOrDefault();

            var data = new TelemetryData
            {
                EventType = EventType.LoadPage,
                EventData = new PageData
                {
                    PageType = roomType,
                    PageNumber = Convert.ToInt32(roomPage)
                }
            };

            await TelemetryJs.LogAsync(data, true);
        }
    }
}