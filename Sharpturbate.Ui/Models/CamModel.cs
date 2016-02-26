using Sharpturbate.Core.Enums;
using Sharpturbate.Core.Models;
using System;

namespace Sharpturbate.Ui.Models
{
    public class CamModel : ChaturbateModel
    {
        public bool IsFavorite { get; set; }

        public CamModel(ChaturbateModel model, bool isFavorite = false)
        {
            ImageSource = model.ImageSource;
            Link = model.Link;
            Room = model.Room;
            StreamName = model.StreamName;
            IsFavorite = isFavorite;
        }

        public CamModel(string modelName, bool isFavorite = true)
        {
            Link = new Uri($"http://chaturbate.com/{modelName}");
            StreamName = modelName;
            ImageSource = new Uri($"https://cdn-s.highwebmedia.com/uHK3McUtGCG3SMFcd4ZJsRv8/roomimage/{modelName}.jpg");
            Room = Rooms.Featured;
        }
    }
}
