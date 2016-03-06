using System;
using System.Diagnostics;
using PostSharp.Aspects;
using Sharpturbate.Core.Aspects.Actions;
using Sharpturbate.Core.Aspects.Parsers;

namespace Sharpturbate.Core.Aspects
{
    [Serializable]
    public class LogDataAttribute : OnMethodBoundaryAspect
    {
        public override void OnEntry(MethodExecutionArgs args)
        {
            Safe.Run(() => DownloadParser.StartInfo(args));
            Safe.Run(() => PageParser.PageInfo(args));
            args.MethodExecutionTag = Stopwatch.StartNew();
        }

        public override void OnExit(MethodExecutionArgs args)
        {
            var executionTime = (Stopwatch) args.MethodExecutionTag;
            Safe.Run(() => DownloadParser.EndInfo(args, executionTime));
            Safe.Run(() => JoinParser.JoinInfo(args));
        }
    }
}