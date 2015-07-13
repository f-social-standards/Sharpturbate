using ChaturbateDownloader.Helpers;
using ChaturbateDownloader.Windows;
using ChaturbateSharp;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
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
    public partial class MainWindow : Window
    {
        public static List<Chaturbate> downloads;
        Rooms type = Rooms.Main;
        System.Windows.Forms.NotifyIcon notificationIcon;
        ChaturbateSettings settings;

        public void UpdateStreamCount()
        {
            ActiveStreamLabel.Text = string.Format("Active streams: {0}", downloads.Count);
        }

        public MainWindow()
        {
            InitializeComponent();
            downloads = new List<Chaturbate>();
            settings = JsonSettings<ChaturbateSettings>.Get();
            PopulateGrid();

            femaleCams.MouseEnter += CategoryHover;
            coupleCams.MouseEnter += CategoryHover;
            transsexualCams.MouseEnter += CategoryHover;
            maleCams.MouseEnter += CategoryHover;
            favoriteCams.MouseEnter += CategoryHover;

            femaleCams.MouseLeave += CategoryExitHover;
            coupleCams.MouseLeave += CategoryExitHover;
            transsexualCams.MouseLeave += CategoryExitHover;
            maleCams.MouseLeave += CategoryExitHover;
            favoriteCams.MouseLeave += CategoryExitHover;

            femaleCams.MouseDown += CategorySelect;
            coupleCams.MouseDown += CategorySelect;
            transsexualCams.MouseDown += CategorySelect;
            maleCams.MouseDown += CategorySelect;
            favoriteCams.MouseDown += CategorySelect;

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
        }

        private void CategoryHover(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ((TextBlock)sender).FontWeight = FontWeights.Bold;
        }

        private void CategoryExitHover(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ((TextBlock)sender).FontWeight = FontWeights.Normal;
        }

        private void CategorySelect(object sender, MouseButtonEventArgs e)
        {
            type = (Rooms)Enum.Parse(typeof(Rooms), ((TextBlock)sender).Text);
            PopulateGrid();
        }

        public async void PopulateGrid()
        {
            StreamContainer.Children.Clear();

            try
            {
                var streams = type == Rooms.Favorites ? await ChaturbateStreams.GetFavoriteStreams(settings) : await ChaturbateStreams.GetStreams(type);
                int column = 0, row = 0;

                foreach (var stream in streams)
                {
                    var image = new Image();
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(stream.Image, UriKind.Absolute);
                    bitmap.EndInit();

                    image.Source = bitmap;
                    image.Width = 120;
                    image.Margin = new Thickness(0, 15, 0, 5);

                    image.MouseDown += (object sender, MouseButtonEventArgs e) =>
                    {
                        var selectedImage = ((Image)sender);
                        if (selectedImage.Opacity > 0.3)
                        {
                            selectedImage.Opacity = 0.3;
                            LinkBox.AppendText(stream.Link + Environment.NewLine);
                        }
                        else
                        {
                            if (LinkBox.Text.Contains(stream.Link))
                            {
                                LinkBox.Text = LinkBox.Text.Replace(stream.Link + Environment.NewLine, "");
                                selectedImage.Opacity = 0.6;
                            }
                            else MessageBox.Show("This stream is already in the download queue...");
                        }
                    };

                    image.MouseEnter += (object sender, MouseEventArgs e) =>
                    {
                        var hoveredImage = ((Image)sender);
                        if (hoveredImage.Opacity == 1)
                            hoveredImage.Opacity = 0.6;
                    };

                    image.MouseLeave += (object sender, MouseEventArgs e) =>
                    {
                        var hoveredImage = ((Image)sender);
                        if (hoveredImage.Opacity == 0.6)
                            hoveredImage.Opacity = 1;
                    };

                    if (downloads.FirstOrDefault(x => x.SiteLink == stream.Link) != null)
                    {
                        image.Opacity = 0.3;
                    }

                    var text = new TextBlock();
                    text.Text = stream.StreamName;
                    text.FontWeight = FontWeights.Bold;
                    text.Foreground = Brushes.Black;
                    text.Margin = new Thickness(20, 0, 0, 0);

                    var favorite = new Image();
                    BitmapImage favBitmap = new BitmapImage();
                    favBitmap.BeginInit();
                    favBitmap.StreamSource = GetStream(ImageFormat.Png);
                    favBitmap.EndInit();

                    favorite.Source = favBitmap;
                    favorite.Width = 15;
                    favorite.Height = 15;
                    favorite.Margin = new Thickness(140, -70, 0, 0);
                    favorite.Opacity = 0.3;

                    if (settings.Models.FirstOrDefault(x => x.StreamName == stream.StreamName) != null)
                        favorite.Opacity = 1;

                    favorite.MouseEnter += (object sender, MouseEventArgs e) => {
                        if (((Image)sender).Opacity == 0.3)
                        {
                            ((Image)sender).Opacity = 0.9;
                        }
                    };

                    favorite.MouseLeave += (object sender, MouseEventArgs e) => {
                        if (((Image)sender).Opacity == 0.9)
                        {
                            ((Image)sender).Opacity = 0.3;
                        }
                    };

                    favorite.MouseDown += (object sender, MouseButtonEventArgs e) => {
                        if (((Image)sender).Opacity != 1)
                        {
                            ((Image)sender).Opacity = 1;
                            settings.Models.Add(stream);
                            JsonSettings<ChaturbateSettings>.Set(settings);
                        }
                        else
                        {
                            var item = settings.Models.FirstOrDefault(x => x.StreamName == stream.StreamName);
                            if(item != null)
                            {
                                settings.Models.Remove(item);
                                ((Image)sender).Opacity = 0.3;
                                JsonSettings<ChaturbateSettings>.Set(settings);
                            }
                        }
                    };

                    Canvas.SetZIndex(favorite, 1000);
                    StreamContainer.Children.Add(text);
                    StreamContainer.Children.Add(favorite);
                    StreamContainer.Children.Add(image);

                    Grid.SetRow(favorite, row);
                    Grid.SetColumn(favorite, column);
                    Grid.SetRow(image, row);
                    Grid.SetColumn(image, column);
                    Grid.SetRow(text, row);
                    Grid.SetColumn(text, column++);
                    if (column == StreamContainer.ColumnDefinitions.Count)
                    {
                        column = 0;
                        row++;
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("No internet connection available. Cannot fetch data from Chaturbate!");
            }
        }

        public Stream GetStream(ImageFormat format)
        {
            var ms = new MemoryStream();
            Properties.Resources.fav.Save(ms, format);
            return ms;
        }

        private void EliminateVideo(string msg, string link)
        {
            MessageBox.Show(msg);
            var video = downloads.FirstOrDefault(x => x.SiteLink == link);
            downloads.Remove(video);
            UpdateStreamCount();
            PopulateGrid();
        }

        private void OnDownload(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new System.Windows.Forms.FolderBrowserDialog();

            if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) 
            {
                var links = LinkBox.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                string downloadPath = saveFileDialog.SelectedPath;
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

            PopulateGrid();
        }

        private void Featured(object sender, RoutedEventArgs e)
        {
            type = Rooms.Main;
            PopulateGrid();
        }

        private void ViewStreams(object sender, RoutedEventArgs e)
        {
            var activityWindow = new ActiveStreams();
            activityWindow.Show();
            activityWindow.OnRemove += (Chaturbate stream) => {
                Dispatcher.Invoke(() => {
                    if(!stream.IsJoiningParts && !stream.IsActive && !stream.IsTimedOut)
                        EliminateVideo(string.Format("Succesfully removed and deleted files for {0}...", stream.ClipName), stream.SiteLink);
                });
            };
        }

        private void OnClose(object sender, System.ComponentModel.CancelEventArgs e)
        {
            foreach (var download in downloads)
            {
                if (download.IsActive)
                    download.RemoveDownload();
            }

            notificationIcon.Visible = false;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == System.Windows.WindowState.Minimized)
            {
                this.Hide();
                notificationIcon.Visible = true;
            }
            else notificationIcon.Visible = false;

            base.OnStateChanged(e);
        }
    }
}
