using FFMpegSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace ChaturbateSharp
{
    public delegate void MessageHandler(string message);

    public class Chaturbate
    {
        public string SiteLink { get; private set; }
        public string StreamLink { get; private set; }
        public string ClipName { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsJoiningParts { get; private set; }
        public bool IsTimedOut { get; private set; }
        public int ActivePart { get; private set; }
        public string ActivityReport {
            get {
                return _messages.ToString();
            } }

        public MessageHandler OnMessage;
        private FFMpeg _ffmpeg;
        private StringBuilder _messages;
        private bool _killed = false,
                     _stopped = false;
        List<string> _clipNames;
        private int _joinTries = 0;
        private DateTime startTime;


        public Chaturbate(string link)
        {
            SiteLink = link;
            _ffmpeg = new FFMpeg();
            _messages = new StringBuilder();
            _clipNames = new List<string>();
            ActivePart = 0;
            ClipName = SiteLink.Split('/')[3];
        }

        public void StartDownload(string path, Action success = null, Action error = null)
        {
            Task.Run(() => {
                startTime = DateTime.Now;
                for (; ;)
                {
                    if (_killed)
                        return;
                    try
                    {
                        if (_stopped)
                            throw new Exception();

                        if (!IsTimedOut)
                            startTime = DateTime.Now;

                        DownloadStream(path);

                        if (_killed)
                        {
                            foreach (var clip in _clipNames)
                                if (File.Exists(clip))
                                    File.Delete(clip);
                            break;
                        }
                    }
                    catch
                    {
                        int streamTimeout = DateTime.Now.Subtract(startTime).Minutes;
                        string minuteCount = DateTime.Now.Subtract(startTime).Minutes.ToString() + ":" + DateTime.Now.Subtract(startTime).Seconds.ToString();


                        if (!IsTimedOut) {
                            string lastVideo = _clipNames.LastOrDefault();
                            SendMessage(string.Format("Stream timed out for {0} after recording {1}...", minuteCount, lastVideo == null ? string.Empty : lastVideo));
                        }

                        IsTimedOut = true;

                        if (streamTimeout > 2 || _stopped)
                        {
                            JoinVideoParts(path, success, error);
                            break;
                        }
                    }
                }

                IsTimedOut = false;
                IsJoiningParts = false;
                IsActive = false;
            });
        }

        public void RemoveDownload()
        {
            try {
                IsTimedOut = false;
                IsJoiningParts = false;
                IsActive = false;
                _ffmpeg.Kill();
                _killed = true;
            }
            catch(Exception e)
            {
                File.WriteAllText(string.Format("exception-{0}", Environment.TickCount), e.Message);
            }
        }

        public void StopDownload()
        {
            try
            {
                _ffmpeg.Stop();
                _stopped = true;
            }
            catch (Exception e)
            {
                File.WriteAllText(string.Format("exception-{0}", Environment.TickCount), e.Message);
            }
        }

        private void SendMessage(string msg)
        {
            if (OnMessage != null)
                OnMessage(msg);

            _messages.AppendLine(msg);
        }

        private void DownloadStream(string path)
        {
            StreamLink = GetChaturbateStreamLink(SiteLink);

            SendMessage(string.Format("Downloading part {0} of {1}...", ActivePart, ClipName));

            string fullClipPath = string.Format(@"{0}\{1}_part_{2}.mp4", path, ClipName, ActivePart++);

            IsActive = true;
            IsTimedOut = false;
            _ffmpeg.SaveM3U8Stream(StreamLink, fullClipPath);
            IsActive = false;

            if (File.Exists(fullClipPath))
                _clipNames.Add(fullClipPath);
        }

        private void JoinVideoParts(string path, Action success, Action error)
        {
            string joinedPath = string.Format(@"{0}\{1}_recorded_on_{2}_{3}.mp4", path, ClipName, DateTime.Now.ToShortDateString(), Environment.TickCount).Replace("/", "_");

            SendMessage(string.Format("Joining {0} temp parts...", _clipNames.Count));

            IsJoiningParts = true;
            _ffmpeg.Join(joinedPath, _clipNames.ToArray());
            _joinTries++;
            IsJoiningParts = false;

            if (File.Exists(joinedPath))
            {
                SendMessage("File saved succesfully !");
                SendMessage("Deleting temp parts...");

                foreach (string clip in _clipNames)
                    File.Delete(clip);

                SendMessage("Temp files cleared, your chaturbate stream has been saved to " + joinedPath);

                if (success != null)
                    success();
            }
            else
            {
                if (_joinTries == 1)
                {
                    var last = _clipNames.Last();

                    if (File.Exists(last))
                        File.Delete(last);

                    _clipNames.Remove(last);

                    JoinVideoParts(path, success, error);
                }
                else
                {
                    SendMessage("Could not join the stream file parts ! File parts have not been deleted.");
                    if (error != null)
                        error();
                }
            }
        }

        private static string GetChaturbateStreamLink(string siteLink)
        {
            string link = "";
            WebRequest request = WebRequest.Create(siteLink);
            using (WebResponse response = request.GetResponse())
            {
                using (StreamReader responseStream = new StreamReader(response.GetResponseStream()))
                {
                    link = responseStream.ReadToEnd().Split(new string[] { "html += \"src='" }, StringSplitOptions.RemoveEmptyEntries)[1].Split(new string[] { "'\";" }, StringSplitOptions.RemoveEmptyEntries)[0];
                }
            }
            return link;
        }
    }
}
