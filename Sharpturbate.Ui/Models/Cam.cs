using System;
using System.IO;
using System.Net;
using NLog;
using Sharpturbate.Core.Enums;
using Sharpturbate.Core.Models;
using Sharpturbate.Ui.Logging;

namespace Sharpturbate.Ui.Models
{
    public class Cam : ChaturbateModel
    {
        public Cam(ChaturbateModel model, bool isFavorite = false)
        {
            ImageSource = model.ImageSource;
            Link = model.Link;
            Room = model.Room;
            StreamName = model.StreamName;
            IsFavorite = isFavorite;
            IsOnline = model.IsOnline;
            IsDownloading = false;
        }

        public Cam(string modelName, bool isFavorite = true)
        {
            Link = new Uri($"http://chaturbate.com/{modelName}");
            StreamName = modelName;
            ImageSource = new Uri($"https://cdn-s.highwebmedia.com/uHK3McUtGCG3SMFcd4ZJsRv8/roomimage/{modelName}.jpg");
            Room = Rooms.Featured;
        }

        public bool IsFavorite { get; set; }
        public bool IsDownloading { get; set; }

        public bool IsNotDownloading => !IsDownloading;

        public bool IsDownloadable => IsOnline && IsNotDownloading;

        public Cam ChangeSource(string filePath)
        {
            ImageSource = new Uri(new FileInfo(filePath).FullName);
            return this;
        }

        public void SaveImage(string cacheLocation)
        {
            try
            {
                string localFile = $"{cacheLocation}\\{StreamName}.png";

                if (File.Exists(localFile)) return;

                var webClient = new WebClient();
                webClient.DownloadFileAsync(ImageSource, localFile);
            }
            catch (Exception e)
            {
                Log.LogEvent(LogLevel.Error, new Error(e));
            }
        }
    }
}