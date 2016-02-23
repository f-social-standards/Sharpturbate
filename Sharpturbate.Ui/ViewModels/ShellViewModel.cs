using Caliburn.Micro;
using Sharpturbate.Core;
using Sharpturbate.Core.Browser;
using Sharpturbate.Core.Enums;
using Sharpturbate.Ui.DataSource;
using Sharpturbate.Ui.Models;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using static Sharpturbate.Ui.Serializer.UserSettings<Sharpturbate.Core.Models.ChaturbateSettings>;

namespace Sharpturbate.Ui.ViewModels
{
    public class ShellViewModel : Conductor<IScreen>
    {
        public ShellViewModel()
        {
            DisplayName = "Sharpturbate";
            DownloadLocation = Settings.DownloadLocation;
            LoadModels();
        }

        public string DownloadLocation { get; set; }

        public IEnumerable<Rooms> Categories { get; set; } = ChaturbateRooms.Categories;

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

        public string StreamURL { get; set; }

        public BindableCollection<CamModel> CamModels { get; set; } = new BindableCollection<CamModel>();

        public BindableCollection<SharpturbateWorker> DownloadQueue { get; set; } = new BindableCollection<SharpturbateWorker>();

        public void DownloadCam()
        {
            if(StreamURL.EndsWith("/"))
            {
                StreamURL = StreamURL.Substring(0, StreamURL.Length - 1);
            }
            var streamName = StreamURL.Split('/').Last();
            CamModel model = new CamModel(new Core.Models.ChaturbateModel() {
                Link = new System.Uri(StreamURL),
                StreamName = streamName,
                ImageSource = new System.Uri(string.Format("https://cdn-s.highwebmedia.com/uHK3McUtGCG3SMFcd4ZJsRv8/roomimage/{0}.jpg", streamName)),
                Room = Rooms.Featured
            });

            DownloadCam(model);
        }

        public void DownloadCam(CamModel cam)
        {
            SharpturbateWorker worker = new SharpturbateWorker(cam);

            worker.OnEvent += (LogType type, string message) => {
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
            LoadContent(type);
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
                Settings.DownloadLocation = DownloadLocation;
        }

        public void OpenSettings()
        {
            ShowSettings = true;
        }

        public void AddFavorite(CamModel model)
        {
            Settings.AddFavorite(model);
        }

        private async void LoadContent(Rooms type = Rooms.Featured, int page = 1)
        {
            CamModels.Clear();
            IsLoaderVisible = Visibility.Visible;
            CamModels.AddRange((await ChaturbateCache.Get(type, page)));
            IsLoaderVisible = Visibility.Hidden;
            ShowSettings = string.IsNullOrWhiteSpace(Settings.DownloadLocation);
        }
    }
}
