using System;

namespace Sharpturbate.Ui.Config
{
    public static class AppSettings
    {
        private static Random Rng { get; } = new Random(Environment.TickCount);

        public static string AppName { get; set; } = "Sharpturbate - Respect DCMA claims";

        public static Uri SafeImage {
            get
            {
                return new Uri($"pack://application:,,,/Images/safe{Rng.Next(1, 9)}.png");
            }
        }
    }
}