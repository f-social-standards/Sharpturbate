using GalaSoft.MvvmLight;
using Sharpturbate.Core.Browser;
using Sharpturbate.Core.Enums;
using Sharpturbate.Core.Models;
using System.Linq;

namespace Sharpturbate.Ui.ViewModel
{
    public class CamViewModel : ViewModelBase
    {
        private ChaturbateModel _cam;

        public ChaturbateModel Cam {
            get
            {
                return _cam;
            }
            set
            {
                _cam = value;
                RaisePropertyChanged("Cam");
            }
        }

        public CamViewModel()
        {
            Cam = ChaturbateProxy.GetStreams(Rooms.Female).Result.FirstOrDefault();
        }
    }
}
