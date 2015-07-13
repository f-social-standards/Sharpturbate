using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ChaturbateSharp
{
    public enum Rooms
    {
        Main,
        Female,
        Transsexual,
        Couple,
        Male,
        Favorites
    }

    public static class ChaturbateStreams
    {
        public static string _url = "http://chaturbate.com";

        public async static Task<List<ChaturbateModel>> GetStreams(Rooms roomType = Rooms.Main)
        {
            Task<List<ChaturbateModel>> getModels = new Task<List<ChaturbateModel>>(() => {
                List<ChaturbateModel> activeModels = new List<ChaturbateModel>();

                var subUrl = roomType == Rooms.Main ? string.Empty : string.Format("/{0}-cams", roomType.ToString());

                WebRequest reqeust = WebRequest.Create(_url + subUrl);

                string[] responseHtml = default(string[]);

                using (var response = reqeust.GetResponse())
                {
                    using (var reader = new StreamReader(response.GetResponseStream()))
                    {
                        responseHtml = reader.ReadToEnd().Replace("</li>", "").Split(new string[] { "<li>" }, StringSplitOptions.RemoveEmptyEntries).Where(x => x.Contains("<img src=") && !x.Contains("LOGIN")).ToArray();
                        foreach (var model in responseHtml)
                        {
                            var split = model.Split('\"');
                            if (split[3].Contains(".jpg"))
                                activeModels.Add(new ChaturbateModel()
                                {
                                    Link = string.Format("{0}{1}", _url, split[1]),
                                    Image = split[3],
                                    StreamName = split[1].Replace("/", ""),
                                    Room = roomType
                                });
                        }
                    }
                }

                return activeModels;
            });

            getModels.Start();

            return await getModels;
        }

        public async static Task<List<ChaturbateModel>> GetFavoriteStreams(ChaturbateSettings settings)
        {
            List<Task<List<ChaturbateModel>>> rooms = new List<Task<List<ChaturbateModel>>>();
            rooms.Add(GetStreams(Rooms.Main));
            rooms.Add(GetStreams(Rooms.Female));
            rooms.Add(GetStreams(Rooms.Male));
            rooms.Add(GetStreams(Rooms.Couple));
            rooms.Add(GetStreams(Rooms.Transsexual));

            List<ChaturbateModel> results = new List<ChaturbateModel>();

            var resultsArray = await Task.WhenAll(rooms);
            foreach (var result in resultsArray)
            {
                var currentRoom = result.First().Room;
                var roomFavorites = settings.Models.Where(x => x.Room == currentRoom);
                foreach (var favorite in roomFavorites)
                {
                    var favoriteOnline = result.FirstOrDefault(x => x.StreamName == favorite.StreamName);
                    if (favoriteOnline != null)
                        results.Add(favoriteOnline);
                }
            }

            return results;
        }
    }
}
