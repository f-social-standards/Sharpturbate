using System;
using System.Collections;
using Telemetry.Net.Interfaces;

namespace Sharpturbate.Core.Telemetry.Models
{
    [Serializable]
    public class Error : IEventData
    {
        public Error(Exception ex)
        {
            Message = ex?.Message;
            StackTrace = ex?.StackTrace;
            Source = ex?.Source;
            Data = ex?.Data;

            if (ex?.InnerException != null)
            {
                InnerException = new Error(ex.InnerException);
            }
        }

        public string Message { get; set; }
        public string StackTrace { get; set; }
        public string Source { get; set; }
        public IDictionary Data { get; set; }
        public Error InnerException { get; set; }
    }
}