using System;

namespace Sharpturbate.Ui.Models
{
    [Serializable]
    public class Error
    {
        public Error() { }

        public Error(Exception ex)
        {
            TimeStamp = DateTime.Now;
            Message = ex.Message;
            StackTrace = ex.StackTrace;
            InnerException = new Error()
            {
                Message = ex.InnerException?.Message,
                StackTrace = ex.InnerException?.StackTrace
            };
        }

        public DateTime TimeStamp { get; set; }
        public string Message { get; set; }
        public string StackTrace { get; set; }
        public Error InnerException { get; set; }
    }
}
