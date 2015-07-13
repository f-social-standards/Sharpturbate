using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChaturbateSharp
{
    public class ChaturbateModel
    {
        public string Image { get; set; }
        public string Link { get; set; }
        public string StreamName { get; set; }
        public Rooms Room { get; set; }
    }
}
