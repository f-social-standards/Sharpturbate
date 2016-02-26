using Caliburn.Micro;
using NLog;
using Sharpturbate.Core;
using Sharpturbate.Core.Browser;
using Sharpturbate.Core.Enums;
using Sharpturbate.Ui.Config;
using Sharpturbate.Ui.DataSource;
using Sharpturbate.Ui.Logging;
using Sharpturbate.Ui.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using static Sharpturbate.Ui.Config.UserSettings<Sharpturbate.Core.Models.ChaturbateSettings>;

namespace Sharpturbate.Ui.ViewModels
{
    public sealed class ShellViewModel : Conductor<IScreen>
    {
        public ShellViewModel()
        {
            TaskbarVisibility = Visibility.Hidden;
            DisplayName = AppSettings.AppName;
            DownloadLocation = Settings.DownloadLocation;
            Interval = 15;
            LoadModels();
        }

        public IEnumerable<Rooms> Categories { get; set; } = ChaturbateRooms.Categories;

        public BindableCollection<Cam> FavoriteModels { get; set; } = new BindableCollection<Cam>();

        public BindableCollection<Cam> CamModels { get; set; } = new BindableCollection<Cam>();

        public BindableCollection<SharpturbateWorker> DownloadQueue { get; set; } = new BindableCollection<SharpturbateWorker>();

        public BindableCollection<Cam> ScheduleQueue { get; set; } = new BindableCollection<Cam>();

        public int ActiveDownloads
        {
            get
            {
                return DownloadQueue.Count(x => x.Status == StreamStatus.Active);
            }
        }

        public int FinishedDownloads
        {
            get
            {
                return DownloadQueue.Count(x => x.Status == StreamStatus.Idle);
            }
        }
        
        public int Interval { get; set; }

        private Cam scheduledModel { get; set; }
        public Cam ScheduledModel
        {
            get
            {
                return scheduledModel;
            }
            set
            {
                scheduledModel = value;
                NotifyOfPropertyChange(() => ScheduledModel);
            }
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

        private bool showSettingsDialog;
        public bool ShowSettingsDialog
        {
            get { return showSettingsDialog; }
            set
            {
                showSettingsDialog = value;
                NotifyOfPropertyChange(() => ShowSettingsDialog);
            }
        }

        private bool showSchedulerDialog;
        public bool ShowSchedulerDialog
        {
            get { return showSchedulerDialog; }
            set
            {
                showSchedulerDialog = value;
                NotifyOfPropertyChange(() => ShowSchedulerDialog);
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

        public WindowState windowState;
        public WindowState WindowState
        {
            get
            {
                return windowState;
            }
            set
            {
                windowState = value;
                NotifyOfPropertyChange(() => WindowState);
            }
        }

        public Visibility windowVisibility;
        public Visibility WindowVisibility
        {
            get
            {
                return windowVisibility;
            }
            set
            {
                windowVisibility = value;
                NotifyOfPropertyChange(() => WindowVisibility);
            }
        }

        public Visibility taskbarVisibility;
        public Visibility TaskbarVisibility
        {
            get
            {
                return taskbarVisibility;
            }
            set
            {
                taskbarVisibility = value;
                NotifyOfPropertyChange(() => TaskbarVisibility);
            }
        }

        private string message;
        public string Message
        {
            get
            {
                return message;
            }
            set
            {
                message = value;
                NotifyOfPropertyChange(() => Message);
            }
        }
        public bool showMessageDialog;
        public bool ShowMessageDialog
        {
            get
            {
                return showMessageDialog;
            }
            set
            {
                showMessageDialog = value;
                NotifyOfPropertyChange(() => ShowMessageDialog);
            }
        }
        public void ShowMessage(string message)
        {
            Message = message;
            ShowMessageDialog = true;
        }

        public void ShowLog(SharpturbateWorker model)
        {            
            var logLines = Regex.Split(model.Log, "\r\n|\r|\n").ToList();

            if (logLines.Count > 10)
            {
                logLines = logLines.Skip(logLines.Count - 10).Take(10).ToList();

                logLines.Insert(0, "...");
            }

            ShowMessage(string.Join(Environment.NewLine, logLines));
        }

        public void ScheduleDownload()
        {
            var schedule = Task.Run(() => {
                Uri streamUri = default(Uri);
                var scheduledCam = ScheduledModel;
                var rng = new Random(Environment.TickCount);

                while (streamUri == null)
                {
                    int timeoutMiliseconds = (Interval + rng.Next(-5, 5)) * 1000;
                    streamUri = ChaturbateProxy.GetStreamLink(scheduledCam);
                    Thread.Sleep(timeoutMiliseconds);
                }

                DownloadCam(scheduledCam);
            });
        }

        public void DownloadCam()
        {
            var streamRegex = @"(http|https):(\/\/chaturbate.com\/)[a-zA-Z0-9._-]+(\/?)";

            var match = Regex.Match(StreamURL, streamRegex);

            if (match.Success)
            {
                if (StreamURL.EndsWith("/"))
                {
                    StreamURL = StreamURL.Substring(0, StreamURL.Length - 1);
                }
                var streamName = StreamURL.Split('/').Last();

                Cam model = new Cam(streamName);

                DownloadCam(model);
            }
            else
            {
                ShowMessage("Please enter a valid stream Url.");
            }
        }

        public void DownloadCam(Cam cam)
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
                Dispatcher.CurrentDispatcher.Invoke(() => {
                    NotifyOfPropertyChange(() => DownloadQueue);
                    NotifyOfPropertyChange(() => DownloadQueue.FirstOrDefault(x => x.Model.StreamName == worker.Model.StreamName).LastUpdate);
                    NotifyOfPropertyChange(() => DownloadQueue.FirstOrDefault(x => x.Model.StreamName == worker.Model.StreamName).Status);
                    NotifyOfPropertyChange(() => DownloadQueue.FirstOrDefault(x => x.Model.StreamName == worker.Model.StreamName).ActivePart);
                });
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
            ShowSettingsDialog = true;
        }

        public void OpenScheduler()
        {
            FavoriteModels.Clear();
            FavoriteModels.AddRange(Settings.Favorites);            
            ShowSchedulerDialog = true;
        }

        public void ToggleFavorite(Cam model)
        { 
            Settings.ToggleFavorite(model);
        }

        public void OnStateChanged()
        {
            if (WindowState == WindowState.Minimized)
            {
                WindowVisibility = Visibility.Hidden;
                TaskbarVisibility = Visibility.Visible;
            }
        }

        public void ShowWindow()
        {
            TaskbarVisibility = Visibility.Hidden;
            WindowVisibility = Visibility.Visible;
            WindowState = WindowState.Maximized;
        }

        private async void LoadContent(Rooms type, int page)
        {
            CamModels.Clear();
            IsLoaderVisible = Visibility.Visible;
            CamModels.AddRange((await ChaturbateCache.Get(type, page)));
            IsLoaderVisible = Visibility.Hidden;
            ShowSettingsDialog = string.IsNullOrWhiteSpace(Settings.DownloadLocation);
        }
    }
}
