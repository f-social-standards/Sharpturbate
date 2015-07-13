using ChaturbateDownloader.Controls;
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
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ChaturbateDownloader.Windows
{
    /// <summary>
    /// Interaction logic for Details.xaml
    /// </summary>
    public delegate void OnDownloadCancel(Chaturbate stream);

    public partial class ActiveStreams : Window
    {
        public OnDownloadCancel OnRemove;

        public ActiveStreams()
        {
            InitializeComponent();
            Icon = Imaging.CreateBitmapSourceFromHBitmap(Properties.Resources.main.ToBitmap().GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            foreach(var download in MainWindow.downloads)
            {
                var entry = new ChaturbateEntry(download);
      
                entry.OnRemove += (Chaturbate removedDownload, ChaturbateEntry element) => {
                    MainWindow.downloads.Remove(removedDownload);
                    StatusStack.Children.Remove(element);
                    if (OnRemove != null)
                        OnRemove(removedDownload);
                };
                
                StatusStack.Children.Add(entry);
            }
        }

        
    }
}
