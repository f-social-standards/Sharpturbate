using Sharpturbate.Core.Browser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharpturbate.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            var r = ChaturbateProxy.GetStreamsAsync(Core.Enums.Rooms.Female).Result;

            var test = new Core.SharpturbateWorker(r.First());

            test.OnEvent += (type, message) => {
                System.Console.WriteLine("{0}: {1}", type, message);
            };

            test.Start(@"E:\");

            System.Console.ReadLine();
        }
    }
}
