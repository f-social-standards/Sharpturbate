using ChaturbateDownloader.Controls;
using ChaturbateDownloader.Helpers;
using ChaturbateSharp;
using MahApps.Metro.Controls;
using MahApps.Metro.Controls.Dialogs;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ChaturbateDownloader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public volatile static List<Chaturbate> downloads = new List<Chaturbate>();
        volatile int scheduleCount = 0;

        Rooms type = Rooms.Main;
        System.Windows.Forms.NotifyIcon notificationIcon;
        ChaturbateSettings settings;
        double lastWindowsWidth = 0;

        public void UpdateStreamCount()
        {
            ActiveStreamLabel.Text = string.Format("Active streams: {0}", downloads.Count);
            ActiveScheduleLabel.Text = string.Format("Active schedules: {0}", scheduleCount);
        }

        public MainWindow()
        {
            InitializeComponent();

            lastWindowsWidth = this.Width;

            settings = JsonSettings<ChaturbateSettings>.Get();

            notificationIcon = new System.Windows.Forms.NotifyIcon();
            notificationIcon.Icon = Properties.Resources.main;
            notificationIcon.Visible = false;

            Icon = Imaging.CreateBitmapSourceFromHBitmap(Properties.Resources.main.ToBitmap().GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());

            notificationIcon.DoubleClick +=
                delegate (object sender, EventArgs args)
                {
                    this.Show();
                    this.WindowState = WindowState.Normal;
                };

            PopulateGrid();
        }

        private void CloseFlyout(object sender, RoutedEventArgs e)
        {
            FlyoutContent.Children.Clear();
        }

        #region Categories
        private void CategoryHover(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var textBlock = ((TextBlock)sender);
            if (textBlock.FontWeight == FontWeights.Normal)
                textBlock.FontWeight = FontWeights.Bold;
            else
                textBlock.FontWeight = FontWeights.Normal;
        }

        private void CategorySelect(object sender, RoutedEventArgs e)
        {
            string roomType = string.Empty;
            if (sender.GetType() == typeof(TextBlock))
            {
                var text = (TextBlock)sender;
                roomType = text.ToolTip.ToString();
            }
            if (sender.GetType() == typeof(Button))
            {
                var button = (Button)sender;
                roomType = button.ToolTip.ToString();
            }
            type = (Rooms)Enum.Parse(typeof(Rooms), roomType);
            PopulateGrid();
        }
        #endregion

        public async void PopulateGrid(object sender = null, RoutedEventArgs e = null)
        {
            StreamContainer.Children.Clear();
            LoadingGrid.IsActive = true;
            try
            {
                var streams = type == Rooms.Favorites ? await ChaturbateStreams.GetFavoriteStreams(settings) : await ChaturbateStreams.GetStreams(type);
                int column = 0, row = 0;
                foreach (var stream in streams)
                {
                    ChaturbateModelContainer chaturbateModel = new ChaturbateModelContainer(stream);

                    if (downloads.FirstOrDefault(x => x.SiteLink == stream.Link) != null)
                        chaturbateModel.IsModelSelected = true;

                    if (settings.Models.FirstOrDefault(x => x.StreamName == stream.StreamName) != null)
                        chaturbateModel.IsFavorite = true;
                    Grid.SetRow(chaturbateModel, row);
                    Grid.SetColumn(chaturbateModel, column++);

                    chaturbateModel.MarkAsFavorite += AddFavorite;

                    chaturbateModel.PlayStream += PlayStream;

                    chaturbateModel.ModelClick += ModelSelect;

                    LoadingGrid.IsActive = false;
                    StreamContainer.Children.Add(chaturbateModel);

                    if (column == StreamContainer.ColumnDefinitions.Count)
                    {
                        column = 0;
                        row++;
                    }
                }
            }
            catch (Exception)
            {
                await this.ShowMessageAsync("Error notification", "Cannot fetch data from Chaturbate!");
            }
        }

        private void AddFavorite(object sender, MouseButtonEventArgs e)
        {
            var chaturbateModel = (ChaturbateModelContainer)sender;
            if (chaturbateModel.IsFavorite)
            {
                settings.Models.Add(chaturbateModel.Model);
                JsonSettings<ChaturbateSettings>.Set(settings);
            }
            else
            {
                var item = settings.Models.FirstOrDefault(x => x.StreamName == chaturbateModel.Model.StreamName);
                if (item != null)
                {
                    settings.Models.Remove(item);
                    JsonSettings<ChaturbateSettings>.Set(settings);
                }
            }
        }

        private async void PlayStream(object sender, MouseButtonEventArgs e)
        {
            var chaturbateModel = (ChaturbateModelContainer)sender;
            chaturbateModel.IsStreamLoading = true;
            
            if (!string.IsNullOrWhiteSpace(chaturbateModel.StreamLink))
            {
                var videoStream = new ChaturbateStreamContainer(chaturbateModel.StreamLink);
                FlyoutContent.Children.Clear();
                ActivityFlyout.Header = string.Format("Streaming {0}", chaturbateModel.Model.StreamName);
                FlyoutContent.Children.Add(videoStream);
                chaturbateModel.IsStreamLoading = false;
                ActivityFlyout.IsOpen = true;
            }
            else await this.ShowMessageAsync("Stream notification", "An error occured while fetching the stream URL.");
        }

        private async void ModelSelect(object sender, MouseButtonEventArgs e)
        {
            var chaturbateModel = (ChaturbateModelContainer)sender;
            if (chaturbateModel.IsModelSelected)
            {
                LinkBox.AppendText(chaturbateModel.Model.Link + Environment.NewLine);
            }
            else
            {
                if (LinkBox.Text.Contains(chaturbateModel.Model.Link))
                {
                    LinkBox.Text = LinkBox.Text.Replace(chaturbateModel.Model.Link + Environment.NewLine, "");
                }
                else await this.ShowMessageAsync("Action notification", "This stream is already in the download queue...");
            }
        }

        private async void EliminateVideo(string msg, string link)
        {
            var video = downloads.FirstOrDefault(x => x.SiteLink == link);
            downloads.Remove(video);
            UpdateStreamCount();
            await this.ShowMessageAsync("Download notification", msg);
        }

        private void OnDownload(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new System.Windows.Forms.FolderBrowserDialog();

            if (!string.IsNullOrWhiteSpace(settings.DefaultPath) || saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) 
            {
                var links = LinkBox.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                string downloadPath = string.IsNullOrWhiteSpace(settings.DefaultPath) ? saveFileDialog.SelectedPath : settings.DefaultPath;
                foreach (string link in links)
                {
                    downloads.Add(new Chaturbate(link));
                    downloads.Last().StartDownload(downloadPath, () => {
                        Dispatcher.Invoke(() => {
                            EliminateVideo(string.Format("Finished downloading {0}.", link), link);
                        });
                    }, () => {
                        EliminateVideo(string.Format("Finished downloading {0}. An error occured while joining video parts.", link), link);
                    });
                }

                UpdateStreamCount();
            }

            LinkBox.Text = string.Empty;
        }

        private void ViewStreams(object sender, RoutedEventArgs e)
        {
            if (!ActivityFlyout.IsOpen)
            {
                ActivityFlyout.IsOpen = true;
                FlyoutContent.Children.Clear();
                ActivityFlyout.Header = "Stream activity";
                foreach (var download in downloads.OrderBy(x => x.ClipName))
                {
                    var entry = new ChaturbateEntry(download);

                    entry.OnRemove += async (Chaturbate removedDownload, ChaturbateEntry element) =>
                    {
                        downloads.Remove(removedDownload);
                        if (FlyoutContent.Children.Contains(element))
                            FlyoutContent.Children.Remove(element);
                        PopulateGrid();
                        await this.ShowMessageAsync("Download notification", string.Format("Succesfully removed {0}", removedDownload.ClipName));
                    };

                    FlyoutContent.Children.Add(entry);
                }
            }
            else ActivityFlyout.IsOpen = false;
        }

        private void OnClose(object sender, System.ComponentModel.CancelEventArgs e)
        {
            foreach (var download in downloads)
            {
                if (download.IsActive)
                    download.StopDownload();
            }
                       
            notificationIcon.Visible = false;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                this.Hide();
                notificationIcon.Visible = true;
            }
            else notificationIcon.Visible = false;

            if (WindowState == WindowState.Maximized)
            {
                Task.Run(() =>
                {
                    Thread.Sleep(150);
                    Dispatcher.Invoke(() =>
                    {
                        RearrangeGridOnSizeChange(null, null);
                    });
                });
            }

            base.OnStateChanged(e);
        }

        private async void SetDefaultDownloadLocation(object sender, RoutedEventArgs e)
        {
            string downloadLocation = "";

            var respone = await this.ShowMessageAsync("Default download location", "Set default download location manually?", MessageDialogStyle.AffirmativeAndNegative);
            
            if(respone == MessageDialogResult.Affirmative)
            {
                downloadLocation = await this.ShowInputAsync("Default download location", "Please input the default");
            }
            else
            {
                var saveFileDialog = new System.Windows.Forms.FolderBrowserDialog();
                if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    downloadLocation = saveFileDialog.SelectedPath;                    
                }
            }

            if (!string.IsNullOrWhiteSpace(downloadLocation))
            {
                settings.DefaultPath = downloadLocation;
                JsonSettings<ChaturbateSettings>.Set(settings);
            }
        }

        private void RearrangeGridOnSizeChange(object sender, SizeChangedEventArgs e)
        {
            scheduleScroll.Height = this.ActualHeight - 50;
            var difference = Math.Abs(lastWindowsWidth - this.ActualWidth);
            if (difference > 180)
            {
                int column = 0, row = 0;
                int columns = (int)this.ActualWidth / 200;
                StreamContainer.ColumnDefinitions.Clear();
                for (int i = 0; i < columns; i++)
                {
                    StreamContainer.ColumnDefinitions.Add(new ColumnDefinition()
                    {
                        Width = new GridLength(200)
                    });
                }

                foreach (var model in StreamContainer.Children)
                {
                    var chaturbateModel = (ChaturbateModelContainer)model;
                    Grid.SetRow(chaturbateModel, row);
                    Grid.SetColumn(chaturbateModel, column++);

                    if (column == StreamContainer.ColumnDefinitions.Count)
                    {
                        column = 0;
                        row++;
                    }
                }

                lastWindowsWidth = this.ActualWidth;
            }
        }

        private void ViewLinkBox(object sender, RoutedEventArgs e)
        {
            StreamFlyout.IsOpen = !StreamFlyout.IsOpen;
        }

        private void ScheduleDownload(object sender, RoutedEventArgs e)
        {
            favoriteModelButtons.Children.Clear();
            var inactiveFavorites = settings.Models
                .Where(x => downloads.Where(d => d.ClipName == x.StreamName)
                .FirstOrDefault() == null)
                .OrderBy(x => x.StreamName);
            foreach (var model in inactiveFavorites)
            {
                var btn = new Button();
                btn.Content = model.StreamName;
                btn.Click += (object senderx, RoutedEventArgs ex) => {
                    streamLink.Text = model.Link;
                };
                favoriteModelButtons.Children.Add(btn);
            }
            ScheduleFlyout.IsOpen = true;
        }

        private async void InitiateScheduler(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(settings.DefaultPath))
                await this.ShowMessageAsync("Settings error", "Please choose a download default location before scheduling a download.");
            else
            {
                string link = streamLink.Text;
                string downloadPath = settings.DefaultPath;
                var schedule = new Task(() =>
                {
                    while (true)
                    {
                        try
                        {
                            Chaturbate.GetChaturbateStreamLink(link);
                            Dispatcher.Invoke(() =>
                            {
                                downloads.Add(new Chaturbate(link));
                                downloads.Last().StartDownload(downloadPath, () =>
                                {
                                    Dispatcher.Invoke(() =>
                                    {
                                        EliminateVideo(string.Format("Finished downloading {0}.", link), link);
                                    });
                                }, () =>
                                {
                                    EliminateVideo(string.Format("Finished downloading {0}. An error occured while joining video parts.", link), link);
                                });
                                scheduleCount--;
                                UpdateStreamCount();
                            });
                            break;
                        }
                        catch (Exception ex)
                        {

                        }
                        Thread.Sleep(900000);
                    }
                });
                scheduleCount++;
                schedule.Start();
                UpdateStreamCount();
            }
        }
    }
}
