using Newtonsoft.Json;
using Sharpturbate.Core.Models;
using Sharpturbate.Ui.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace Sharpturbate.Ui.Config
{
    public static class UserSettings<T> where T : ChaturbateSettings, new()
    {
        public static class Settings
        {
            private static string LocalDirectory = "local";            
            private static string CacheDirectory = $"{LocalDirectory}\\cache\\images";
            private static string LogDirectory = $"{LocalDirectory}\\logs";

            private static string SettingsPath = $"{LocalDirectory}\\settings.json";

            static Settings()
            {
                if(!Directory.Exists(LocalDirectory))
                {
                    Directory.CreateDirectory(LocalDirectory);
                }

                if (!Directory.Exists(CacheDirectory))
                {
                    Directory.CreateDirectory(CacheDirectory);
                }

                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }
            }

            private static T loadedSettings = null;

            private static T Current
            {
                get
                {
                    if (loadedSettings == null)
                    {
                        if (File.Exists(SettingsPath))
                        {
                            string settings = File.ReadAllText(SettingsPath);
                            loadedSettings = JsonConvert.DeserializeObject<T>(settings);
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
                    return Current.Models.Select(x => new CamModel(x, true));
                }
            }

            public static void ToggleFavorite(CamModel cam)
            {
                var favorite = Current.Models.FirstOrDefault(x => x.StreamName == cam.StreamName);

                if (favorite != null)
                {
                    Current.Models.Remove(favorite);
                }
                else
                {
                    if (!Directory.Exists(CacheDirectory))
                        Directory.CreateDirectory(CacheDirectory);

                    string localFile = $"{CacheDirectory}\\{cam.StreamName}.png";

                    if (!File.Exists(localFile))
                    {
                        WebClient webClient = new WebClient();
                        webClient.DownloadFile(cam.ImageSource, localFile);
                    }

                    Current.Models.Add(cam);
                }

                Save();
            }

            private static void Save()
            {
                string settings = JsonConvert.SerializeObject(Current);
                File.WriteAllText(SettingsPath, settings);
            }
        }
    }
}
