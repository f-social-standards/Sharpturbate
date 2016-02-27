using System;
using System.Collections.Generic;
using System.Linq;

namespace Sharpturbate.Core.Enums
{
    public enum Rooms
    {
        Featured = 1,
        Female,
        Transsexual,
        Couple,
        Male,
        Favorites
    }

    public static class ChaturbateRooms
    {
        public static IEnumerable<Rooms> Categories { get { return Enum.GetValues(typeof(Rooms)).Cast<Rooms>(); } }
    }
}
