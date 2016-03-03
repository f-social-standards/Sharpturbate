using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sharpturbate.Core.Browser;
using Sharpturbate.Core.Enums;
using Sharpturbate.Core.Models;
using Sharpturbate.Ui.Extensions;
using Sharpturbate.Ui.Models;
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
        private const int CacheTimeout = 180;

        private static readonly Func<Rooms, int, Task<IEnumerable<ChaturbateModel>>> GetModels =
            async (roomType, pageNumber) =>
            {
                var cams = roomType == Rooms.Favorites
                    ? await ChaturbateProxy.GetFavorites(Settings.Current)
                    : await ChaturbateProxy.GetStreamsAsync(roomType, pageNumber);

                if (roomType != Rooms.Favorites) return cams;

                foreach (var cam in cams.Where(cam => cam.IsOnline))
                {
                    new Cam(cam, true).SaveImage(Settings.CacheDirectory);
                }

                return cams;
            };

        static ChaturbateCache()
        {
            const int sleepFor = CacheTimeout * 1000;

            Task.Run(() =>
            {
                Thread.Sleep(sleepFor);
                var itemsToRemove =
                    Cache.Where(x => DateTime.Now.Subtract(x.Value.Timestamp).TotalSeconds > CacheTimeout).ToArray();
                foreach (var entry in itemsToRemove)
                {
                    CacheEntry<IEnumerable<ChaturbateModel>> outVar;
                    Cache.TryRemove(entry.Key, out outVar);
                }
            });
        }

        public static Rooms CurrentRoom { get; private set; } = Rooms.Featured;
        public static int CurrentPage { get; private set; }

        private static ConcurrentDictionary<long, CacheEntry<IEnumerable<ChaturbateModel>>> Cache { get; } =
            new ConcurrentDictionary<long, CacheEntry<IEnumerable<ChaturbateModel>>>();

        public static async Task<IEnumerable<Cam>> Get(Rooms type, int page = 1)
        {
            CurrentRoom = type;
            CurrentPage = page;

            var key = ((long) page << 32) + (int) type;

            if (!Cache.ContainsKey(key))
            {
                var response = await GetModels(type, page);

                var success = false;

                while (!success)
                {
                    success = Cache.TryAdd(key, new CacheEntry<IEnumerable<ChaturbateModel>>
                    {
                        Timestamp = DateTime.Now,
                        Value = response
                    });
                }
            }
            else
            {
                if (DateTime.Now.Subtract(Cache[key].Timestamp).TotalSeconds > CacheTimeout)
                {
                    var response = await GetModels(type, page);

                    Cache[key].Timestamp = DateTime.Now;
                    Cache[key].Value = response;
                }
            }

            return
                Cache[key].Value.WithCache()
                    .Select(x => new Cam(x, Settings.Favorites.Any(model => model.StreamName == x.StreamName)));
        }
    }
}