using System;
using Sharpturbate.Core.Enums;

namespace Sharpturbate.Core.Models
{
    public class ChaturbateModel
    {
        public Uri ImageSource { get; set; }
        public Uri Link { get; set; }
        public string StreamName { get; set; }
        public Rooms Room { get; set; }
        public bool IsOnline { get; set; }
    }
}