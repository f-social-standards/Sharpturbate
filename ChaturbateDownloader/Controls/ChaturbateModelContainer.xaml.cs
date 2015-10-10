using ChaturbateSharp;
using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ChaturbateDownloader.Controls
{
    /// <summary>
    /// Interaction logic for ChaturbateModel.xaml
    /// </summary>
    public partial class ChaturbateModelContainer : UserControl
    {
        public MouseButtonEventHandler ModelClick;
        public MouseButtonEventHandler MarkAsFavorite;
        public MouseButtonEventHandler PlayStream;
        public EventHandler ModelLoaded;
        public ChaturbateModel Model;
        double unselected = 0.5;
        public string StreamLink { get; set; }

        public bool IsStreamLoading
        {
            get
            {
                return loadingVideo.IsActive;
            }
            set
            {
                loadingVideo.IsActive = value;
            }
        }

        public bool IsModelSelected
        {
            get
            {
                return modelImage.Opacity == unselected;
            }
            set
            {
                if (value)
                    modelImage.Opacity = unselected;
                else modelImage.Opacity = 1;
            }
        }

        public bool IsFavorite
        {
            get
            {
                return favorite.Opacity == 1;
            }
            set
            {
                if (value)
                    favorite.Opacity = 1;
                else favorite.Opacity = unselected;
            }
        }

        public ChaturbateModelContainer(ChaturbateModel model)
        {
            Model = model;
            InitializeComponent();
            InitializeFavorite();
            InitializePlayBack();
            InitializeModelImage(Model.Image);
            modelName.Text = Model.StreamName;
            modelName.FontWeight = FontWeights.Bold;
            modelName.Foreground = Brushes.Black;
        }

        private void InitializeFavorite()
        {
            favorite.MouseEnter += (object sender, MouseEventArgs e) => {
                var element = ((UIElement)sender);
                if (element.Opacity == unselected)
                {
                    element.Opacity = 0.9;
                }
            };

            favorite.MouseLeave += (object sender, MouseEventArgs e) => {
                var element = ((UIElement)sender);
                if (element.Opacity == 0.9)
                {
                    element.Opacity = unselected;
                }
            };

            favorite.MouseDown += (object sender, MouseButtonEventArgs e) => {
                IsFavorite = !IsFavorite;
                if (MarkAsFavorite != null)
                    MarkAsFavorite(this, e);
            };
        }

        private void InitializePlayBack()
        {
            play.MouseEnter += (object sender, MouseEventArgs e) => {
                var element = (UIElement)sender;
                if (element.Opacity == unselected)
                {
                    element.Opacity = 1;
                }
            };

            play.MouseLeave += (object sender, MouseEventArgs e) => {
                var element = (UIElement)sender;
                if (element.Opacity == 1)
                {
                    element.Opacity = unselected;
                }
            };

            play.MouseDown += async (object sender, MouseButtonEventArgs e) => {
                if (PlayStream != null)
                {
                    loadingVideo.IsActive = true;
                    if(string.IsNullOrWhiteSpace(StreamLink))
                        StreamLink = await Task.Run(() =>
                        {
                            try {
                                return Chaturbate.GetChaturbateStreamLink(Model.Link);
                            }
                            catch
                            {
                                return "";
                            }
                        });
                    loadingVideo.IsActive = false;
                    PlayStream(this, e);
                }
            };
        }

        private void InitializeModelImage(string imgSource)
        {
            BitmapImage bitmap = new BitmapImage(new Uri(imgSource, UriKind.Absolute));
            bitmap.DownloadCompleted += DownloadCompleted; ;
            modelImage.Source = bitmap;

            modelImage.MouseEnter += (object sender, MouseEventArgs e) =>
            {
                var hoveredImage = ((Image)sender);
                if (hoveredImage.Opacity == 1)
                    hoveredImage.Opacity = 0.6;
            };

            modelImage.MouseLeave += (object sender, MouseEventArgs e) =>
            {
                var hoveredImage = ((Image)sender);
                if (hoveredImage.Opacity == 0.6)
                    hoveredImage.Opacity = 1;
            };

            modelImage.MouseDown += (object sender, MouseButtonEventArgs e) =>
            {
                IsModelSelected = !IsModelSelected;
                if (ModelClick != null)
                    ModelClick(this, e);
            };
        }
        private void DownloadCompleted(object sender, EventArgs e)
        {
            if (ModelLoaded != null)
                ModelLoaded(this, e);
        }
    }
}
