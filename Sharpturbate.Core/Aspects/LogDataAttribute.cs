using System;
using System.Diagnostics;
using Sharpturbate.Core.Aspects.Actions;
using Sharpturbate.Core.Aspects.Parsers;
using ArxOne.MrAdvice.Advice;

namespace Sharpturbate.Core.Aspects
{
    [Serializable]
    public class LogDataAttribute : Attribute, IMethodAdvice
    {
        public void Advise(MethodAdviceContext context)
        {
            Safe.Run(() => DownloadParser.StartInfo(context));
            Safe.Run(() => PageParser.PageInfo(context));
            var executionTime = Stopwatch.StartNew();
            context.Proceed();
            Safe.Run(() => DownloadParser.EndInfo(context, executionTime));
            Safe.Run(() => JoinParser.JoinInfo(context));
        }
    }
}