using Caliburn.Micro;
using NLog;
using Sharpturbate.Ui.Logging;
using Sharpturbate.Ui.Models;
using Sharpturbate.Ui.Serializer;
using Sharpturbate.Ui.ViewModels;
using System.Windows;
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
            DisplayRootViewFor<ShellViewModel>();
        }

        protected override void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Instance.Log(LogLevel.Error, Serialize.JSON.Serialize(new Error(e.Exception)));
            e.Handled = true;
        }
    }
}
