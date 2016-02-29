using Caliburn.Micro;
using NLog;
using Sharpturbate.Ui.Logging;
using Sharpturbate.Ui.Models;
using Sharpturbate.Ui.Properties;
using Sharpturbate.Ui.ViewModels;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Sharpturbate.Ui
{
    public class AppBootstrapper : BootstrapperBase
    {
        public AppBootstrapper()
        {
            Initialize();
        }

        protected override void OnStartup(object sender, StartupEventArgs e)
        {
            BitmapSource icon = Imaging.CreateBitmapSourceFromHBitmap(
                Resources.sharpturbate.ToBitmap().GetHbitmap(), 
                IntPtr.Zero, 
                Int32Rect.Empty, 
                BitmapSizeOptions.FromEmptyOptions());

            DisplayRootViewFor<ShellViewModel>(new Dictionary<string, object>
            {
                { "Icon", icon }
            });
        }

        protected override void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.LogEvent(LogLevel.Error, new Error(e.Exception));
            e.Handled = true;
        }
    }
}
