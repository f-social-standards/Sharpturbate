using Telemetry.Net.Interfaces;

namespace Sharpturbate.Core.Telemetry.Models
{
    public class JoinData : IEventData
    {
        public string ModelName { get; set; }
        public int Duration { get; set; }
        public double Size { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}