using FFMpegSharp;
using FFMpegSharp.FFMPEG;
using Sharpturbate.Core.Browser;
using Sharpturbate.Core.Enums;
using Sharpturbate.Core.Extensions;
using Sharpturbate.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sharpturbate.Core
{
    public delegate void LogHandler(LogType type, string message);

    public class SharpturbateWorker
    {
        #region Private Region
        private volatile IList<string> clipParts;
        private VideoInfo[] goodParts
        {
            get
            {
                IList<VideoInfo> parts = new List<VideoInfo>();
                int corruptParts = 0;
                foreach(var clip in clipParts)
                {
                    try
                    {
                        parts.Add(new VideoInfo(clip));
                    }
                    catch
                    {
                        corruptParts++;
                    }
                }

                if(corruptParts > 0)
                    LogProgress(LogType.Warning, string.Format("Removing {0} parts out of {1}, because of corruption. Joining only healthy video parts.", corruptParts, clipParts.Count));

                return parts.ToArray();
            }
        }

        private volatile StringBuilder log;
        private volatile Uri uri;

        private volatile FFMpeg ffmpeg;
        private volatile bool removed = false,
                              stopped = false;

        private static readonly int _allowedTimeout = 2;

        private void LogProgress(LogType type, string message)
        {
            var logEntry = string.Format("{0}: {1}", type, message);

            if (OnEvent != null)
                OnEvent(type, logEntry);

            LastUpdate = logEntry;

            log.AppendLine(logEntry);
        }
        #endregion

        public LogHandler OnEvent;
        public ChaturbateModel Model { get; set; }
        public StreamStatus Status { get; set; }
        public int ActivePart { get; private set; }
        public string Log { get { return log.ToString();  } }
        public string LastUpdate { get; set; }
        public SharpturbateWorker(ChaturbateModel model)
        {
            Model = model;
            clipParts = new List<string>();
            ffmpeg = new FFMpeg();
            log = new StringBuilder();
        }

        public void Start(string outputPath)
        {
            Stopwatch timeoutWatch = default(Stopwatch);

            Task.Run(() => 
            {
                try
                {
                    uri = ChaturbateProxy.GetStreamLink(Model);
                    for (;;)
                    {
                        if (removed || Status == StreamStatus.Idle)
                        {
                            LogProgress(LogType.Update, "Show recored succesfully for {0}.");

                            foreach (string clip in clipParts)
                                File.Delete(clip);

                            LogProgress(LogType.Success, "Temporary files cleared succesfully.");
                        }

                        // if the stream is active
                        if (uri.IsAvailable() && !stopped && !removed)
                        {
                            Status = StreamStatus.Active;
                            LogProgress(LogType.Update, string.Format("Starting to download {0} part {1}...", Model.StreamName, ActivePart));

                            // download the stream
                            string downloadPath = string.Format(@"{0}\{1}_part_{2}.mp4", outputPath, Model.StreamName, ActivePart++);
                            ffmpeg.SaveM3U8Stream(uri, new FileInfo(downloadPath));

                            // check if the file exists after the recording is finished
                            if (File.Exists(downloadPath))
                                clipParts.Add(downloadPath);
                            else ActivePart--;
                        }
                        else
                        {
                            // notify timeout
                            if (Status != StreamStatus.TimeOut)
                            {
                                timeoutWatch = Stopwatch.StartNew();
                                LogProgress(LogType.Warning, string.Format("Stream timed out for after recording part {0}...", ActivePart - 1));
                                Status = StreamStatus.TimeOut;
                            }

                            // check if it has timed out for long enough or if the process was stopped
                            if (timeoutWatch.Elapsed.TotalMinutes > _allowedTimeout || stopped)
                            {
                                string finalOutputPath = string.Format(@"{0}\{1}_recorded_on_{2}_{3}.mp4", outputPath, Model.StreamName, DateTime.Now.ToString("MM_dd_yyyy"), DateTime.Now.Ticks);
                                
                                Status = StreamStatus.Joining;

                                LogProgress(LogType.Update, string.Format("Joining {0} temporary parts for {1}...", clipParts.Count, Model.StreamName));
                                // join only good video stream parts
                                ffmpeg.Join(new FileInfo(finalOutputPath), goodParts);

                                if (File.Exists(finalOutputPath))
                                {
                                    Status = StreamStatus.Idle;
                                }
                                else
                                {
                                    LogProgress(LogType.Warning, string.Format("File parts could not be joined."));
                                    return;
                                }
                            }
                        }
                    }
                }
                catch(Exception e)
                {
                    LogProgress(LogType.Error, string.Format("Something happened while downloading the stream. Info: {0}", e.Message));
                }
            });
        }

        public void Stop()
        {
            try
            {
                stopped = true;
                ffmpeg.Stop();
                LogProgress(LogType.Update, "Downloaded parts are queued to be joined.");
            }
            catch(Exception e)
            {
                LogProgress(LogType.Error, string.Format("An error occured while stopping the stream. Details: {0}", e.Message));
            }
        }

        public void Delete()
        {
            removed = true;
            ffmpeg.Kill();

            if(ffmpeg.IsKillFaulty)
            {
                LogProgress(LogType.Error, "The FFMpeg process suffered a faulty kill.");
            }
            else
            {
                LogProgress(LogType.Update, "Downloaded parts are queued for removal.");
            }
        }
    }
}
