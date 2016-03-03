using System.Linq;
using Sharpturbate.Core;
using Sharpturbate.Core.Browser;
using Sharpturbate.Core.Enums;

namespace Sharpturbate.Console
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var r = ChaturbateProxy.GetStreamsAsync(Rooms.Female).Result;

            var test = new SharpturbateWorker(r.First());

            test.OnEvent += (type, message) => { System.Console.WriteLine("{0}: {1}", type, message); };

            test.Start(@"E:\");

            System.Console.ReadLine();
        }
    }
}