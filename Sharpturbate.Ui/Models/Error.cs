using Newtonsoft.Json;
using System;
using System.Collections;

namespace Sharpturbate.Ui.Models
{
    [Serializable]
    public class Error
    {
        public Error() { }

        public Error(Exception ex)
        {
            Message = ex?.Message;
            StackTrace = ex?.StackTrace;
            Source = ex?.Source;
            Data = ex?.Data;

            if(ex.InnerException != null)
            {
                InnerException = new Error(ex.InnerException);
            }
        }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Message { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string StackTrace { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Source { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public IDictionary Data { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Error InnerException { get; set; }
    }
}
