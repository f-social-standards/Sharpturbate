using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sharpturbate.Core.Models;
using Sharpturbate.Ui.Config;
using Sharpturbate.Ui.Models;

namespace Sharpturbate.Ui.Extensions
{
    public static class CamExtensions
    {
        public static IEnumerable<Cam> WithCache<T>(this IEnumerable<T> camList) where T : ChaturbateModel
        {
            var cache = UserSettings<ChaturbateSettings>.Settings.CacheDirectory;
            return camList.Select(x => File.Exists($"{cache}\\{x.StreamName}.png") && !x.IsOnline
                ? new Cam(x, true).ChangeSource($"{cache}\\{x.StreamName}.png")
                : new Cam(x, true));
        }
    }
}