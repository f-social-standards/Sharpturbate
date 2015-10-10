using FFMpegSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Telemetry.Core;
using Telemetry.Enums;
using Telemetry.Models;

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
                TelemetryDetails details;
                Stopwatch timer = Stopwatch.StartNew(),
                          partTimer = Stopwatch.StartNew();

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

                        string partPath = DownloadStream(path);

                        #region  DownloadFinishedTelemetry
                        details = ChaturbateTelemetry.Config();
                        details.EventType = ChaturbateEventType.DownloadFinished;
                        details.EventData.Duration = (int)partTimer.Elapsed.TotalMinutes;
                        details.EventData.DurationUnit = DurationUnit.Minutes;
                        bool fileExists = File.Exists(partPath);
                        details.EventData.Message = string.Format(fileExists ? "Finished download of part {0}." : "Part {0} was skipped.", ActivePart - 1);
                        if (fileExists)
                        {
                            FileInfo file = new FileInfo(partPath);
                            details.EventData.Size = (file.Length / 1048576.0);
                            details.EventData.SizeUnit = SizeUnit.MB;
                        }
                        details.EventData.PointOfInterest = ClipName;
                        TelemetryJS.LogTelemetry(details);
                        partTimer.Restart();
                        #endregion


                        if (_killed)
                        {
                            foreach (var clip in _clipNames)
                                if (File.Exists(clip))
                                    File.Delete(clip);
                            #region DownloadStopTelemetry
                            timer.Stop();
                            details = ChaturbateTelemetry.Config();
                            details.EventType = ChaturbateEventType.DownloadStop;
                            details.EventData.Duration = (int)timer.Elapsed.TotalMinutes;
                            details.EventData.DurationUnit = DurationUnit.Minutes;
                            details.EventData.Message = string.Format("Deleted {0} parts of clip {1}", _clipNames.Count, ClipName);
                            details.EventData.PointOfInterest = ClipName;

                            TelemetryJS.LogTelemetry(details);
                            #endregion
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
                            string joinedFile = JoinVideoParts(path, success, error);
                            #region DownloadJoinTelemetry
                            timer.Stop();
                            details = ChaturbateTelemetry.Config();
                            details.EventType = ChaturbateEventType.DownloadJoin;
                            details.EventData.Duration = (int)timer.Elapsed.TotalMinutes;
                            details.EventData.DurationUnit = DurationUnit.Minutes;
                            details.EventData.PointOfInterest = ClipName;
                            if (File.Exists(joinedFile))
                            {
                                details.EventData.Message = string.Format("Finished joining {0} parts.", ActivePart);
                                FileInfo file = new FileInfo(joinedFile);
                                details.EventData.Size = (file.Length / 1048576.0);
                                details.EventData.SizeUnit = SizeUnit.MB;
                            }
                            else details.EventData.Message = string.Format("Could not join {0} parts.", ActivePart);

                            TelemetryJS.LogTelemetry(details);
                            #endregion
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
                TelemetryDetails details = ChaturbateTelemetry.Config();
                details.EventType = EventType.Exception;
                details.EventData.Exception.Message = e.Message;
                details.EventData.Exception.CallStack = e.StackTrace ?? "";
                details.EventData.Message = "An exception occured while removing the download.";
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
                TelemetryDetails details = ChaturbateTelemetry.Config();
                details.EventType = EventType.Exception;
                details.EventData.Exception.Message = e.Message;
                details.EventData.Exception.CallStack = e.StackTrace ?? "";
                details.EventData.Message = "An exception occured while stoping the download.";
                TelemetryJS.LogTelemetry(details);
            }
        }

        private void SendMessage(string msg)
        {
            if (OnMessage != null)
                OnMessage(msg);

            _messages.AppendLine(msg);
        }

        private string DownloadStream(string path)
        {
            StreamLink = GetChaturbateStreamLink(SiteLink);

            #region  DownloadStartTelemetry
            var details = ChaturbateTelemetry.Config();
            details.EventType = ChaturbateEventType.DownloadStart;
            details.EventData.Message = string.Format("Starting download of part {0}.", ActivePart);
            details.EventData.PointOfInterest = ClipName;
            TelemetryJS.LogTelemetry(details);
            #endregion

            SendMessage(string.Format("Downloading part {0} of {1}...", ActivePart, ClipName));

            string fullClipPath = string.Format(@"{0}\{1}_part_{2}.mp4", path, ClipName, ActivePart++);

            IsActive = true;
            IsTimedOut = false;
            _ffmpeg.SaveM3U8Stream(StreamLink, fullClipPath);
            IsActive = false;

            if (File.Exists(fullClipPath))
                _clipNames.Add(fullClipPath);

            return fullClipPath;
        }

        private string JoinVideoParts(string path, Action success, Action error)
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

            return joinedPath;
        }

        public static string GetChaturbateStreamLink(string siteLink)
        {
            try {
                Stopwatch timer = Stopwatch.StartNew();
                string link = "";
                TelemetryDetails details = ChaturbateTelemetry.Config();
                WebRequest request = WebRequest.Create(siteLink);
                using (WebResponse response = request.GetResponse())
                {
                    using (StreamReader responseStream = new StreamReader(response.GetResponseStream()))
                    {
                        link = responseStream.ReadToEnd().Split(new string[] { "html += \"src='" }, StringSplitOptions.RemoveEmptyEntries)[1].Split(new string[] { "'\";" }, StringSplitOptions.RemoveEmptyEntries)[0];
                    }
                }
                timer.Stop();
                #region LinkParsed
                timer.Stop();
                details = ChaturbateTelemetry.Config();
                details.EventType = ChaturbateEventType.ParseURL;
                details.EventData.Duration = (int)timer.Elapsed.TotalMilliseconds;
                details.EventData.DurationUnit = DurationUnit.Miliseconds;
                details.EventData.Message = "Parsed download link.";
                details.EventData.PointOfInterest = siteLink;

                TelemetryJS.LogTelemetry(details);
                #endregion
                return link;
            }
            catch(WebException e)
            {
                TelemetryDetails details = ChaturbateTelemetry.Config();
                details.EventType = EventType.Exception;
                details.EventData.Exception.Message = e.Message;
                details.EventData.Exception.CallStack = e.StackTrace ?? "";
                details.EventData.Message = "An exception occured while parsing the download link.";
                TelemetryJS.LogTelemetry(details);
                throw e;
            }
        }
    }
}
