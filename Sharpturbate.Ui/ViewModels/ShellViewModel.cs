using Caliburn.Micro;
using NLog;
using Sharpturbate.Core;
using Sharpturbate.Core.Enums;
using Sharpturbate.Ui.DataSource;
using Sharpturbate.Ui.Logging;
using Sharpturbate.Ui.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using static Sharpturbate.Ui.Config.UserSettings<Sharpturbate.Core.Models.ChaturbateSettings>;

namespace Sharpturbate.Ui.ViewModels
{
    public sealed class ShellViewModel : Conductor<IScreen>
    {
        public ShellViewModel()
        {
            DisplayName = "Sharpturbate";
            DownloadLocation = Settings.DownloadLocation;
            LoadModels();
        }

        private string downloadLocation;
        public string DownloadLocation
        {
            get
            {
                return downloadLocation;
            }
            set
            {
                downloadLocation = value;
                NotifyOfPropertyChange(() => DownloadLocation);
            }
        }

        private Visibility isLoaderVisible;
        public Visibility IsLoaderVisible
        {
            get { return isLoaderVisible; }
            set
            {
                isLoaderVisible = value;
                NotifyOfPropertyChange(() => IsLoaderVisible);
            }
        }

        private bool showSettings;
        public bool ShowSettings
        {
            get { return showSettings; }
            set
            {
                showSettings = value;
                NotifyOfPropertyChange(() => ShowSettings);
            }
        }

        private bool showScheduler;
        public bool ShowScheduler
        {
            get { return showScheduler; }
            set
            {
                showScheduler = value;
                NotifyOfPropertyChange(() => ShowScheduler);
            }
        }

        private int currentPage = 1;
        public int CurrentPage
        {
            get
            {
                return currentPage;
            }
            private set
            {
                currentPage = value;
                NotifyOfPropertyChange(() => CurrentPage);
            }
        }

        private string streamUrl;
        public string StreamURL
        {
            get
            {
                return streamUrl;
            }
            set
            {
                streamUrl = value;
                NotifyOfPropertyChange(() => StreamURL);
            }
        }

        public BindableCollection<string> FavoriteModels { get; set; }

        public string ScheduledModel { get; set; }

        public IEnumerable<Rooms> Categories { get; set; } = ChaturbateRooms.Categories;

        public BindableCollection<CamModel> CamModels { get; set; } = new BindableCollection<CamModel>();

        public BindableCollection<SharpturbateWorker> DownloadQueue { get; set; } = new BindableCollection<SharpturbateWorker>();

        public void DownloadCam()
        {
            var streamRegex = @"(http|https):(\/\/chaturbate.com\/)[A-Za-z0-9@#%^&*]+(\/?)";

            var match = Regex.Match(StreamURL, streamRegex);

            if (match.Success)
            {
                if (StreamURL.EndsWith("/"))
                {
                    StreamURL = StreamURL.Substring(0, StreamURL.Length - 1);
                }
                var streamName = StreamURL.Split('/').Last();

                CamModel model = new CamModel(streamName);

                DownloadCam(model);
            }
        }

        public void DownloadCam(CamModel cam)
        {
            SharpturbateWorker worker = new SharpturbateWorker(cam);

            worker.OnEvent += (LogType type, string message) => {

                LogLevel level = default(LogLevel);

                switch(type)
                {
                    case LogType.Success:
                    case LogType.Update:
                        level = LogLevel.Info; break;
                    case LogType.Error:
                        level = LogLevel.Error; break;
                    case LogType.Warning:
                        level = LogLevel.Warn; break;
                }

                Log.Instance.Log(level, message);

                NotifyOfPropertyChange(null);
            };

            worker.Start(DownloadLocation);

            DownloadQueue.Add(worker);
        }

        public void Stop(SharpturbateWorker worker)
        {
            worker.Stop();
            DownloadQueue.Remove(worker);
        }

        public void Delete(SharpturbateWorker worker)
        {
            worker.Delete();
            DownloadQueue.Remove(worker);
        }

        public void LoadModels(Rooms type = Rooms.Featured)
        {
            CurrentPage = 1;
            LoadContent(type, CurrentPage);
        }

        public void ChangePage(int increment)
        { 
            CurrentPage += increment;

            if (CurrentPage < 1)
                CurrentPage = 1;

            LoadContent(ChaturbateCache.CurrentRoom, CurrentPage);
        }

        public void SaveSettings()
        {
            if(!string.IsNullOrWhiteSpace(DownloadLocation))
                Config.UserSettings<Core.Models.ChaturbateSettings>.Settings.DownloadLocation = DownloadLocation;
        }

        public void OpenSettings()
        {
            ShowSettings = true;
        }

        public void OpenScheduler()
        {
            FavoriteModels = new BindableCollection<string>(Settings.Favorites.Select(x => x.StreamName));
            ShowScheduler = true;
        }

        public void ToggleFavorite(CamModel model)
        {
            Config.UserSettings<Core.Models.ChaturbateSettings>.Settings.ToggleFavorite(model);
        }

        private async void LoadContent(Rooms type, int page)
        {
            CamModels.Clear();
            IsLoaderVisible = Visibility.Visible;
            CamModels.AddRange((await ChaturbateCache.Get(type, page)));
            IsLoaderVisible = Visibility.Hidden;
            ShowSettings = string.IsNullOrWhiteSpace(Settings.DownloadLocation);
        }
    }
}
