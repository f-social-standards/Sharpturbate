using Telemetry.Net.Interfaces;

namespace Sharpturbate.Core.Telemetry.Models
{
    public class PageData : IEventData
    {
        public string PageType { get; set; }
        public int PageNumber { get; set; }
    }
}