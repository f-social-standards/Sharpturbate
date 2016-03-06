using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Caliburn.Micro;
using NLog;
using Sharpturbate.Core;
using Sharpturbate.Core.Browser;
using Sharpturbate.Core.Enums;
using Sharpturbate.Ui.Config;
using Sharpturbate.Ui.DataSource;
using Sharpturbate.Ui.Logging;
using Sharpturbate.Ui.Models;
using Sharpturbate.Ui.RegularExpressions;
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
            MoveToFolder = Settings.MoveToFolder;
            LoadModels();
        }

        public IEnumerable<Rooms> Categories { get; set; } = ChaturbateRooms.Categories;

        public BindableCollection<Cam> FavoriteModels { get; set; } = new BindableCollection<Cam>();

        public BindableCollection<Cam> CamModels { get; set; } = new BindableCollection<Cam>();

        public BindableCollection<SharpturbateWorker> DownloadQueue { get; set; } =
            new BindableCollection<SharpturbateWorker>();

        public BindableCollection<Cam> ScheduleQueue { get; set; } = new BindableCollection<Cam>();

        public int ActiveDownloads
        {
            get { return DownloadQueue.Count(x => x.Status == StreamStatus.Active); }
        }

        public int FinishedDownloads
        {
            get { return DownloadQueue.Count(x => x.Status == StreamStatus.Idle); }
        }

        public int ScheduledDownloads => ScheduleQueue.Count;

        public int Interval { get; set; }

        public Cam ScheduledModel
        {
            get { return _scheduledModel; }
            set
            {
                _scheduledModel = value;
                NotifyOfPropertyChange(() => ScheduledModel);
            }
        }

        public string DownloadLocation
        {
            get { return _downloadLocation; }
            set
            {
                _downloadLocation = value;
                NotifyOfPropertyChange(() => DownloadLocation);
            }
        }

        public bool MoveToFolder
        {
            get { return _moveToFolder; }
            set
            {
                _moveToFolder = value;
                NotifyOfPropertyChange(() => MoveToFolder);
            }
        }

        public Visibility IsLoaderVisible
        {
            get { return _isLoaderVisible; }
            set
            {
                _isLoaderVisible = value;
                NotifyOfPropertyChange(() => IsLoaderVisible);
            }
        }

        public bool ShowSettingsDialog
        {
            get { return _showSettingsDialog; }
            set
            {
                _showSettingsDialog = value;
                NotifyOfPropertyChange(() => ShowSettingsDialog);
            }
        }

        public bool ShowSchedulerDialog
        {
            get { return _showSchedulerDialog; }
            set
            {
                _showSchedulerDialog = value;
                NotifyOfPropertyChange(() => ShowSchedulerDialog);
            }
        }

        public bool IsPaged => ChaturbateCache.CurrentRoom != Rooms.Favorites;

        public int CurrentPage
        {
            get { return _currentPage; }
            private set
            {
                _currentPage = value;
                NotifyOfPropertyChange(() => CurrentPage);
            }
        }

        public string StreamUrl
        {
            get { return _streamUrl; }
            set
            {
                _streamUrl = value;
                NotifyOfPropertyChange(() => StreamUrl);
            }
        }

        public WindowState WindowState
        {
            get { return _windowState; }
            set
            {
                _windowState = value;
                NotifyOfPropertyChange(() => WindowState);
            }
        }

        public Visibility WindowVisibility
        {
            get { return _windowVisibility; }
            set
            {
                _windowVisibility = value;
                NotifyOfPropertyChange(() => WindowVisibility);
            }
        }

        public Visibility TaskbarVisibility
        {
            get { return _taskbarVisibility; }
            set
            {
                _taskbarVisibility = value;
                NotifyOfPropertyChange(() => TaskbarVisibility);
            }
        }

        public string Message
        {
            get { return _message; }
            set
            {
                _message = value;
                NotifyOfPropertyChange(() => Message);
            }
        }

        public bool ShowMessageDialog
        {
            get { return _showMessageDialog; }
            set
            {
                _showMessageDialog = value;
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
            var logLines = Regex.Split(model.Log, RegexCollection.NewLine).ToList();

            if (logLines.Count > 10)
            {
                logLines = logLines.Skip(logLines.Count - 10).Take(10).ToList();

                logLines.Insert(0, "...");
            }

            ShowMessage(string.Join(Environment.NewLine, logLines));
        }

        public void ScheduleDownload()
        {
            ScheduleQueue.Add(ScheduledModel);
            NotifyOfPropertyChange(() => ScheduledDownloads);

            var schedule = Task.Run(() =>
            {
                var streamUri = default(Uri);
                var scheduledCam = ScheduledModel;
                var rng = new Random(Environment.TickCount);

                while (streamUri == null)
                {
                    var timeoutMiliseconds = (Interval + rng.Next(-5, 5))*1000;
                    streamUri = ChaturbateProxy.GetStreamLink(scheduledCam);
                    Thread.Sleep(timeoutMiliseconds);
                }

                ScheduleQueue.Remove(scheduledCam);
                NotifyOfPropertyChange(() => ScheduledDownloads);
                DownloadCam(scheduledCam);
            });
        }

        public void DownloadCam()
        {
            var match = Regex.Match(StreamUrl, RegexCollection.ChaturbateUrl);

            if (match.Success)
            {
                if (StreamUrl.EndsWith("/"))
                {
                    StreamUrl = StreamUrl.Substring(0, StreamUrl.Length - 1);
                }
                var streamName = StreamUrl.Split('/').Last();

                var model = new Cam(streamName);

                DownloadCam(model);
            }
            else
            {
                ShowMessage("Please enter a valid stream Url.");
            }
        }

        public void DownloadCam(Cam cam)
        {
            if (DownloadQueue.Any(x => x.Model.StreamName == cam.StreamName) ||
                ScheduleQueue.Any(x => x.StreamName == cam.StreamName))
            {
                ShowMessage("The stream you are trying to download is already in the schedule or download queue.");
                return;
            }

            cam.IsDownloading = true;
            CamModels.Refresh();

            var worker = new SharpturbateWorker(cam);

            worker.OnEvent += (LogType type, string message) =>
            {
                var level = default(LogLevel);

                switch (type)
                {
                    case LogType.Success:
                    case LogType.Update:
                        level = LogLevel.Info;
                        break;
                    case LogType.Error:
                        level = LogLevel.Error;
                        break;
                    case LogType.Warning:
                        level = LogLevel.Warn;
                        break;
                }

                DownloadQueue.Refresh();
                NotifyOfPropertyChange(() => ActiveDownloads);
                NotifyOfPropertyChange(() => FinishedDownloads);
                Log.LogEvent(level, message);
            };

            worker.Start(DownloadLocation, Settings.Current.MoveToFolder);
            DownloadQueue.Add(worker);
        }

        public void Remove(SharpturbateWorker worker)
        {
            DownloadQueue.Remove(worker);
            DownloadQueue.Refresh();
        }

        public async void Stop(SharpturbateWorker worker)
        {
            if (await worker.StopAsync())
            {
                DownloadQueue.Remove(worker);
                DownloadQueue.Refresh();
            }
            else
            {
                ShowMessage(
                    "It's been some time now... the worker won't stop downloading. It must be 'hard' for him too, if you know what I mean.");
                Log.LogEvent(LogLevel.Warn, "Worker did not stop in a timely manner.");
            }
        }

        public async void Delete(SharpturbateWorker worker)
        {
            if (await worker.DeleteAsync())
            {
                DownloadQueue.Remove(worker);
                DownloadQueue.Refresh();
            }
            else
            {
                ShowMessage("Apparently it's really 'hard' to let go after so much effort put into it.");
                Log.LogEvent(LogLevel.Warn, "Worker did not remove the partial data in a timely manner.");
            }
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
            if (!string.IsNullOrWhiteSpace(DownloadLocation))
            {
                Settings.DownloadLocation = DownloadLocation;
                Settings.MoveToFolder = MoveToFolder;
            }
        }

        public void OpenSettings()
        {
            ShowSettingsDialog = true;
        }

        public void OpenScheduler()
        {
            FavoriteModels.Clear();
            FavoriteModels.AddRange(Settings.Favorites.Where(x =>
                DownloadQueue.All(q => q.Model.StreamName != x.StreamName) &&
                ScheduleQueue.All(sq => sq.StreamName != x.StreamName)));
            ShowSchedulerDialog = true;
        }

        public void ToggleFavorite(Cam model)
        {
            Settings.ToggleFavorite(model);
        }

        public void OnStateChanged()
        {
            if (WindowState != WindowState.Minimized) return;

            WindowVisibility = Visibility.Hidden;
            TaskbarVisibility = Visibility.Visible;
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
            CamModels.AddRange((await ChaturbateCache.Get(type, page)).Select(x =>
            {
                x.IsDownloading = DownloadQueue.Any(q => q.Model.StreamName == x.StreamName);
                return x;
            }));
            IsLoaderVisible = Visibility.Hidden;
            ShowSettingsDialog = string.IsNullOrWhiteSpace(Settings.DownloadLocation);
            NotifyOfPropertyChange(() => IsPaged);
        }

        public override async void CanClose(Action<bool> callback)
        {
            WindowVisibility = Visibility.Hidden;

            foreach (var worker in DownloadQueue.Where(worker => worker.IsWorking))
            {
                await worker.StopAsync();
            }

            callback(true);

            base.CanClose(callback);
        }

        #region Private Members

        private int _currentPage = 1;
        private string _downloadLocation;
        private string _message;
        private bool _showMessageDialog;
        private bool _showSchedulerDialog;
        private bool _showSettingsDialog;
        private bool _moveToFolder;
        private string _streamUrl;
        private Visibility _taskbarVisibility;
        private WindowState _windowState;
        private Visibility _windowVisibility;
        private Visibility _isLoaderVisible;
        private Cam _scheduledModel { get; set; }

        #endregion
    }
}