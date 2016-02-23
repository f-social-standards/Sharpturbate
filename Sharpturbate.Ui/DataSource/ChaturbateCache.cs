using Sharpturbate.Core.Browser;
using Sharpturbate.Core.Enums;
using Sharpturbate.Ui.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sharpturbate.Ui.DataSource
{
    public static class ChaturbateCache
    {
        public static Rooms CurrentRoom { get; private set; } = Rooms.Male;
        public static int CurrentPage { get; private set; } = 0;
        private static List<CamModel> Models { get; set; } = new List<CamModel>();

        public static async Task<IEnumerable<CamModel>> Get(Rooms type, int page = 1)
        {
            if(type != CurrentRoom || CurrentPage != page)
            {
                Models.Clear();
                CurrentRoom = type;
                CurrentPage = page;
                var response = await ChaturbateProxy.GetStreamsAsync(type, page);

                foreach(var model in response)
                {
                    Models.Add(new CamModel(model));
                }                
            }

            return Models;
        }
    }
}
