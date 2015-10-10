using ChaturbateSharp;
using System;
using System.Collections.Generic;
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
    /// Interaction logic for ChaturbateEntry.xaml
    /// </summary>
    public delegate void RemoveEvent(Chaturbate removedDownload, ChaturbateEntry entry);
    public partial class ChaturbateEntry : UserControl
    {
        Chaturbate _download;
        public RemoveEvent OnRemove;
      
        public ChaturbateEntry(Chaturbate download)
        {
            InitializeComponent();
            _download = download;
            chaturbateStar.Text = _download.ClipName;
            logButton.Click += ViewLog;
            removeButton.Click += RemoveDownload;
            if (download.IsActive)
            {
                StatusLed.ToolTip = "Downloading...";
                StatusLed.Fill = (SolidColorBrush)(new BrushConverter().ConvertFrom("#16a085"));
            }
            else
            {
                StatusLed.ToolTip = "Stream inactive...";
                StatusLed.Fill = (SolidColorBrush)(new BrushConverter().ConvertFrom("#c0392b"));
            }

            if (download.IsJoiningParts)
            {
                StatusLed.Fill = (SolidColorBrush)(new BrushConverter().ConvertFrom("#2c3e50"));
                StatusLed.ToolTip = "Joining files...";
            }
            
        }

        private void RemoveDownload(object sender, RoutedEventArgs e)
        {
            _download.RemoveDownload();
            if (OnRemove != null)
            {
                OnRemove(_download, this);
            }
        }

        private void ViewLog(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(_download.ActivityReport);
        }

        private void StopAndJoin(object sender, RoutedEventArgs e)
        {
            _download.StopDownload();
            if (OnRemove != null)
            {
                OnRemove(_download, this);
            }
        }
    }
}
