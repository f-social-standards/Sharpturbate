using Sharpturbate.Core.Models;
using Sharpturbate.Ui.Models;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Sharpturbate.Ui.Serializer
{
    public static class UserSettings<T> where T : ChaturbateSettings, new()
    {
        public static class Settings
        {
            private static string Path = "settings.json";
            private static T loadedSettings = null;

            public static T Current
            {
                get
                {
                    if (loadedSettings == null)
                    {
                        if (File.Exists(Path))
                        {
                            string settings = File.ReadAllText(Path);
                            loadedSettings = Serialize.JSON.Deserialize<T>(settings);
                        }
                        else
                        {
                            loadedSettings = new T();
                        }
                    }

                    return loadedSettings;
                }
            }

            public static string DownloadLocation
            {
                get
                {
                    return Current.DefaultPath;
                }
                set
                {
                    loadedSettings.DefaultPath = value;
                    Save();
                }
            }

            public static IEnumerable<CamModel> Favorites
            {
                get
                {
                    return loadedSettings.Models.Select(x => new CamModel(x));
                }
            }

            public static void AddFavorite(CamModel cam)
            {
                loadedSettings.Models.Add(cam);
                Save();
            }

            public static void RemoveFavorite(CamModel cam)
            {
                loadedSettings.Models.Remove(cam);
                Save();
            }

            private static void Save()
            {
                string settings = Serialize.JSON.Serialize(loadedSettings);
                File.WriteAllText(Path, settings);
            }
        }
    }
}
