using System;
using System.Diagnostics;
using System.Linq;
using Sharpturbate.Core.Telemetry.Enums;
using Sharpturbate.Core.Telemetry.Models;
using Telemetry.Net.Core;
using Telemetry.Net.DataModel;
using ArxOne.MrAdvice.Advice;

namespace Sharpturbate.Core.Aspects.Parsers
{
    public static class DownloadParser
    {
        public const string MethodName = "DownloadStream";

        public static async void StartInfo(MethodAdviceContext args)
        {
            if (args.TargetMethod.Name != MethodName) return;

            var data = ExtractData(args);
            data.EventType = EventType.StartDownload;
            await TelemetryJs.LogAsync(data, true);
        }

        public static async void EndInfo(MethodAdviceContext args, Stopwatch time)
        {
            if (args.TargetMethod.Name != MethodName) return;

            var data = ExtractData(args, time);
            data.EventType = EventType.FinishDownload;
            await TelemetryJs.LogAsync(data, true);
        }

        public static TelemetryData ExtractData(MethodAdviceContext args, Stopwatch time = null)
        {
            var data = new TelemetryData();

            var fileName = args.Arguments[0].ToString().Split('\\').Last();
            var info = fileName.Split(new[] {"_part_"}, StringSplitOptions.None);

            var modelName = info.First();
            var partNumer = int.Parse(info.Last().Split('.').First());
            var elapsedTime = time?.Elapsed.TotalMinutes;

            data.EventData = new DownloadData
            {
                ModelName = modelName,
                PartNumber = partNumer,
                ProcessDuration = elapsedTime
            };

            return data;
        }
    }
}