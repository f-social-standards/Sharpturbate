using System;

namespace Sharpturbate.Core.Aspects.Actions
{
    public static class Safe
    {
        public static void Run(Action action)
        {
            try
            {
                action();
            }
            catch
            {
            }
        }
    }
}