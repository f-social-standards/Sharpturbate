using System.Web.Script.Serialization;

namespace Sharpturbate.Ui.Serializer
{
    public static class Serialize
    {
        public static JavaScriptSerializer JSON { get; set; } = new JavaScriptSerializer();
    }
}
