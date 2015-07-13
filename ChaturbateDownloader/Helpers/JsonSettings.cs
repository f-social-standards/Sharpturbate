using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace ChaturbateDownloader.Helpers
{
    public static class JsonSettings<T> where T : class, new()
    {
        public static string Path = "settings.json";
        static JavaScriptSerializer JSON = new JavaScriptSerializer();
       
        public static T Get()
        {
            if (File.Exists(Path))
            {
                string serialized = File.ReadAllText(Path);
                return JSON.Deserialize<T>(serialized);
            }

            return new T();
        }

        public static void Set(T settings)
        {
            string serialized = JSON.Serialize(settings);
            File.WriteAllText(Path, serialized);
        }
    }
}
