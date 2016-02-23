using Sharpturbate.Core.Models;

namespace Sharpturbate.Ui.Models
{
    public class CamModel : ChaturbateModel
    {
        public CamModel(ChaturbateModel model)
        {
            this.ImageSource = model.ImageSource;
            this.Link = model.Link;
            this.Room = model.Room;
            this.StreamName = model.StreamName;
        }
    }
}
