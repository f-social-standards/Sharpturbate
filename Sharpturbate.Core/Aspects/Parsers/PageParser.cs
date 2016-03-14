using System;
using System.Linq;
using Sharpturbate.Core.Telemetry.Enums;
using Sharpturbate.Core.Telemetry.Models;
using Telemetry.Net.Core;
using Telemetry.Net.DataModel;
using ArxOne.MrAdvice.Advice;

namespace Sharpturbate.Core.Aspects.Parsers
{
    public static class PageParser
    {
        public const string MethodName = "GetStreamsAsync";

        public static async void PageInfo(MethodAdviceContext args)
        {
            if (args.TargetMethod.Name != MethodName) return;

            var roomType = args.Parameters.FirstOrDefault()?.ToString();
            var roomPage = (int?) args.Parameters.LastOrDefault();

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