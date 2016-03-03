using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Sharpturbate.Core.Models;
using Sharpturbate.Ui.Extensions;
using Sharpturbate.Ui.Models;
using Sharpturbate.Ui.RegularExpressions;

namespace Sharpturbate.Ui.Config
{
    public static class UserSettings<T> where T : ChaturbateSettings, new()
    {
        public static class Settings
        {
            public static string LocalDirectory = "local";
            public static string CacheDirectory = $"{LocalDirectory}\\cache\\images";
            public static string LogDirectory = $"{LocalDirectory}\\logs";

            private static readonly string SettingsPath = $"{LocalDirectory}\\settings.json";

            private static T loadedSettings;

            static Settings()
            {
                if (!Directory.Exists(LocalDirectory))
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

            public static T Current
            {
                get
                {
                    if (loadedSettings != null) return loadedSettings;

                    if (File.Exists(SettingsPath))
                    {
                        var settings = File.ReadAllText(SettingsPath);

                        if (Regex.IsMatch(settings, RegexCollection.Base64))
                            settings = Encoding.UTF8.GetString(Convert.FromBase64String(settings));

                        loadedSettings = JsonConvert.DeserializeObject<T>(settings);
                    }
                    else
                    {
                        loadedSettings = new T();
                    }

                    return loadedSettings;
                }
            }

            public static string DownloadLocation
            {
                get { return Current.DefaultPath; }
                set
                {
                    loadedSettings.DefaultPath = value;
                    Save();
                }
            }

            public static IEnumerable<Cam> Favorites
            {
                get { return Current.Models.WithCache().OrderBy(x => x.StreamName); }
            }

            public static void ToggleFavorite(Cam cam)
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

                    cam.SaveImage(CacheDirectory);

                    Current.Models.Add(cam);
                }

                Save();
            }

            private static void Save()
            {
                var settings = JsonConvert.SerializeObject(Current);

                settings = Convert.ToBase64String(Encoding.UTF8.GetBytes(settings));

                File.WriteAllText(SettingsPath, settings);
            }
        }
    }
}