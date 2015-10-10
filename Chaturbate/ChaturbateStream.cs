using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Telemetry.Core;
using Telemetry.Enums;

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
        private static string siteLink;

        public async static Task<List<ChaturbateModel>> GetStreams(Rooms roomType = Rooms.Main, bool log = true)
        {
            Task<List<ChaturbateModel>> getModels = new Task<List<ChaturbateModel>>(() => {
                Stopwatch timer = Stopwatch.StartNew();
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
                #region ParseStreamTelemetry
                if (log)
                {
                    var details = ChaturbateTelemetry.Config();
                    details = ChaturbateTelemetry.Config();
                    details.EventType = ChaturbateEventType.ParseStreams;
                    details.EventData.Duration = (int)timer.Elapsed.TotalMilliseconds;
                    details.EventData.DurationUnit = DurationUnit.Miliseconds;
                    details.EventData.Message = "Parsed streams.";
                    details.EventData.PointOfInterest = roomType.ToString();

                    TelemetryJS.LogTelemetry(details);
                }
                #endregion
                return activeModels;
            });

            getModels.Start();

            return await getModels;
        }

        public async static Task<List<ChaturbateModel>> GetFavoriteStreams(ChaturbateSettings settings)
        {
            List<Task<List<ChaturbateModel>>> rooms = new List<Task<List<ChaturbateModel>>>();
            rooms.Add(GetStreams(Rooms.Female, false));
            rooms.Add(GetStreams(Rooms.Male, false));
            rooms.Add(GetStreams(Rooms.Couple, false));
            rooms.Add(GetStreams(Rooms.Transsexual, false));

            List<ChaturbateModel> results = new List<ChaturbateModel>();

            var resultsArray = await Task.WhenAll(rooms);
            var roomFavorites = settings.Models;

            foreach (var result in resultsArray)
            {
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
