using ChaturbateSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ChaturbateDownloader.Controls
{
    /// <summary>
    /// Interaction logic for ChaturbateStreamContainer.xaml
    /// </summary>
    public partial class ChaturbateStreamContainer : UserControl
    {
        public bool IsActive { get; set; }
        bool bufferStarted = false;

        public ChaturbateStreamContainer(string modelUrl)
        {
            InitializeComponent();
            InitializePlayer(modelUrl);
        }

        public void InitializePlayer(string streamUrl)
        {
            try
            {
                mediaStreamer.UnloadedBehavior = MediaState.Manual;
                mediaStreamer.LoadedBehavior = MediaState.Manual;
                mediaStreamer.Source = null;
                mediaStreamer.Source = new Uri(streamUrl);
                mediaStreamer.Play();
                Mute(null, null);
                IsActive = true;
            }
            catch
            {
                IsActive = false;
            }
        }

        private void BufferEnded(object sender, RoutedEventArgs e)
        {
            loadingStream.IsActive = false;
        }

        private void BufferStarted(object sender, RoutedEventArgs e)
        {
            loadingStream.IsActive = true;
            if (bufferStarted)
            {
                mediaStreamer.Close();
                InitializePlayer(mediaStreamer.Source.AbsoluteUri);
                bufferStarted = false;
            }
            bufferStarted = true;
        }

        private void Mute(object sender, RoutedEventArgs e)
        {
            muteStream.Visibility = Visibility.Collapsed;
            unmuteStream.Visibility = Visibility.Visible;
            mediaStreamer.IsMuted = true;
        }

        private void Unmute(object sender, RoutedEventArgs e)
        {
            muteStream.Visibility = Visibility.Visible;
            unmuteStream.Visibility = Visibility.Collapsed;
            mediaStreamer.IsMuted = false;
        }
    }
}
