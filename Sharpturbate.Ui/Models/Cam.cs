using Sharpturbate.Core.Enums;
using Sharpturbate.Core.Models;
using System;
using System.IO;

namespace Sharpturbate.Ui.Models
{
    public class Cam : ChaturbateModel
    {
        public bool IsFavorite { get; set; }
        public bool IsDownloading { get; set; }
        public bool IsNotDownloading
        {
            get
            {
                return !IsDownloading;
            }
        }

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

        public Cam ChangeSource(string filePath)
        {
            ImageSource = new Uri(new FileInfo(filePath).FullName);
            return this;
        }
    }
}
