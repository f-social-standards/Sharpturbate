using Sharpturbate.Core.Browser;
using Sharpturbate.Core.Enums;
using Sharpturbate.Core.Models;
using Sharpturbate.Ui.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Sharpturbate.Ui.Config.UserSettings<Sharpturbate.Core.Models.ChaturbateSettings>;

namespace Sharpturbate.Ui.DataSource
{
    public class CacheEntry<T>
    {
        public DateTime Timestamp { get; set; }
        public T Value { get; set; }
    }

    public static class ChaturbateCache
    {
        public static Rooms CurrentRoom { get; private set; } = Rooms.Featured;
        public static int CurrentPage { get; private set; } = 0;

        private static Dictionary<long, CacheEntry<IEnumerable<ChaturbateModel>>> Cache { get; set; } = new Dictionary<long, CacheEntry<IEnumerable<ChaturbateModel>>>();

        public static async Task<IEnumerable<CamModel>> Get(Rooms type, int page = 1)
        {
            CurrentRoom = type;
            CurrentPage = page;

            long key = ((long)(page) << 32) + (int)type;

            if(!Cache.ContainsKey(key))
            {
                var response = await ChaturbateProxy.GetStreamsAsync(type, page);

                Cache.Add(key, new CacheEntry<IEnumerable<ChaturbateModel>>
                {
                    Timestamp = DateTime.Now,
                    Value = response
                });
            }
            else
            {
                if(DateTime.Now.Subtract(Cache[key].Timestamp).TotalSeconds > 60)
                {
                    var response = await ChaturbateProxy.GetStreamsAsync(type, page);

                    Cache[key].Timestamp = DateTime.Now;
                    Cache[key].Value = response;
                }
            }

            return Cache[key].Value.Select(x => new CamModel(x, Settings.Favorites.Any(model => model.StreamName == x.StreamName)));
        }
    }
}
