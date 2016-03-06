using Telemetry.Net.Interfaces;

namespace Sharpturbate.Core.Telemetry.Models
{
    public class DownloadData : IEventData
    {
        public string ModelName { get; set; }
        public int PartNumber { get; set; }
        public double? ProcessDuration { get; set; }
    }
}