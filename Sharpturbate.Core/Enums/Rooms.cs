using System;
using System.Collections.Generic;
using System.Linq;

namespace Sharpturbate.Core.Enums
{
    public enum Rooms
    {
        Featured,
        Female,
        Transsexual,
        Couple,
        Male
    }

    public static class ChaturbateRooms
    {
        public static IEnumerable<Rooms> Categories { get { return Enum.GetValues(typeof(Rooms)).Cast<Rooms>(); } }
    }
}
