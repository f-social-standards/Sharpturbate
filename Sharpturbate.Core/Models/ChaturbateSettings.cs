using System.Collections.Generic;

namespace Sharpturbate.Core.Models
{
    public class ChaturbateSettings
    {
        public string DefaultPath = string.Empty;
        public List<ChaturbateModel> Models = new List<ChaturbateModel>();
        public bool MoveToFolder { get; set; }
        public bool IsSafeMode { get; set; }
    }
}