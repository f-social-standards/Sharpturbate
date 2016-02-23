using System.Collections.Generic;

namespace Sharpturbate.Core.Models
{
    public class ChaturbateSettings
    {
        public List<ChaturbateModel> Models = new List<ChaturbateModel>();
        public string DefaultPath = string.Empty;
    }
}
