﻿using AngleSharp;
using AngleSharp.Parser.Html;
using Sharpturbate.Core.Enums;
using Sharpturbate.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sharpturbate.Core.Browser
{
    public static class ChaturbateProxy
    {
        private static string _baseUrl = "http://chaturbate.com";
        private static string _streamExtension = ".m3u8";
        private static Regex _regExp = new Regex(@"(http|https)?:\/\/[a-zA-Z0-9.:\/_-]*\" + _streamExtension, RegexOptions.Compiled);

        private static IConfiguration config = Configuration.Default.WithDefaultLoader();

        public static async Task<Uri> GetStreamLinkAsync(ChaturbateModel model)
        {
            var request = WebRequest.Create(model.Link.AbsoluteUri);

            using (var response = await request.GetResponseAsync())
            {
                var document = new HtmlParser().Parse(response.GetResponseStream());

                var streamScript = document.Scripts.FirstOrDefault(x => x.InnerHtml.Contains(_streamExtension));

                var result = _regExp.Match(streamScript.InnerHtml).Value;

                return new Uri(result);
            }
        }

        public static Uri GetStreamLink(ChaturbateModel model)
        {
            try
            {
                var request = WebRequest.Create(model.Link.AbsoluteUri);

                using (var response = request.GetResponse())
                {
                    var document = new HtmlParser().Parse(response.GetResponseStream());

                    var streamScript = document.Scripts.FirstOrDefault(x => x.InnerHtml.Contains(_streamExtension));

                    var result = _regExp.Match(streamScript.InnerHtml).Value;

                    return new Uri(result);
                }
            }
            catch
            {
                return null;
            }
        }

        public static async Task<IEnumerable<ChaturbateModel>> GetStreamsAsync(Rooms room = Rooms.Featured, int page = 1)
        {
            string roomUrl = string.Empty;

            if (room != Rooms.Featured)
            {
                roomUrl = string.Format("{0}/{1}-cams/?page={2}", _baseUrl, room.ToString().ToLower(), page);
            }
            else
            {
                roomUrl = string.Format("{0}/?page={1}", _baseUrl, page);
            }

            var request = WebRequest.Create(roomUrl);
            using (var response = await request.GetResponseAsync())
            {
                var document = new HtmlParser().Parse(response.GetResponseStream());

                return document.QuerySelectorAll("li")
                               .Where(x => x.QuerySelector("img") != null &&
                                           x.QuerySelector("a") != null)
                               .Select((model) =>
                               {
                                   string subUrl = model.QuerySelector("a").GetAttribute("href"),
                                                   modelUrl = string.Format(string.Format("{0}{1}", _baseUrl, subUrl)),
                                                   imageUrl = model.QuerySelector("img").GetAttribute("src"),
                                                   streamName = subUrl.Replace("/", string.Empty);

                                   return new ChaturbateModel
                                   {
                                       Link = new Uri(modelUrl),
                                       ImageSource = new Uri(imageUrl),
                                       StreamName = streamName,
                                       Room = room,
                                       IsOnline = true
                                   };
                               });
            }
        }

        public static async Task<IEnumerable<ChaturbateModel>> GetFavorites(ChaturbateSettings settings)
        {
            var aggregatedResults = (await Task.WhenAll(GetStreamsAsync(Rooms.Female), 
                                             GetStreamsAsync(Rooms.Male), 
                                             GetStreamsAsync(Rooms.Couple), 
                                             GetStreamsAsync(Rooms.Transsexual))).SelectMany(x => x).Select(x => x);

            return settings.Models.Select(x =>
            {
                x.IsOnline = aggregatedResults.Any(result => result.StreamName == x.StreamName);

                return x;
            }).OrderBy(x => x.IsOnline);
        }
    }
}
