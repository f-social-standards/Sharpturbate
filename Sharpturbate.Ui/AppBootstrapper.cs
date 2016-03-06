using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Caliburn.Micro;
using NLog;
using Sharpturbate.Core.Telemetry.Models;
using Sharpturbate.Ui.Logging;
using Sharpturbate.Ui.Properties;
using Sharpturbate.Ui.ViewModels;

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
            var icon = Imaging.CreateBitmapSourceFromHBitmap(
                Resources.sharpturbate.ToBitmap().GetHbitmap(),
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            DisplayRootViewFor<ShellViewModel>(new Dictionary<string, object>
            {
                {"Icon", icon}
            });
        }

        protected override void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.LogEvent(LogLevel.Error, new Error(e.Exception));
            e.Handled = true;
        }
    }
}