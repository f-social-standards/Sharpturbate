using System;
using System.IO;
using System.Linq;
using FFMpegSharp;
using Sharpturbate.Core.Telemetry.Enums;
using Sharpturbate.Core.Telemetry.Models;
using Telemetry.Net.Core;
using Telemetry.Net.DataModel;
using ArxOne.MrAdvice.Advice;

namespace Sharpturbate.Core.Aspects.Parsers
{
    public class JoinParser
    {
        public const string MethodName = "JoinPartialDownloads";

        public static async void JoinInfo(MethodAdviceContext args)
        {
            if (args.TargetMethod.Name != MethodName) return;

            var path = args.Arguments.FirstOrDefault()?.ToString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;

            var info = new VideoInfo(path);
            var parts = info.Name.Split(new[] {"_recorded_"}, StringSplitOptions.None);
            var modelName = parts.First();

            var data = new TelemetryData
            {
                EventType = EventType.JoinDownload,
                EventData = new JoinData
                {
                    ModelName = modelName,
                    Duration = (int) info.Duration.TotalMinutes,
                    Width = info.Width,
                    Height = info.Height,
                    Size = info.Size
                }
            };

            await TelemetryJs.LogAsync(data, true);
        }
    }
}