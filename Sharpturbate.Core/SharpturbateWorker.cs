using FFMpegSharp;
using FFMpegSharp.FFMPEG;
using FFMpegSharp.FFMPEG.Exceptions;
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
using System.Threading;
using System.Threading.Tasks;

namespace Sharpturbate.Core
{
    public delegate void LogHandler(LogType type, string message);

    public sealed class SharpturbateWorker : IDisposable
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

        private volatile Stopwatch totalStreamTime;
        private volatile StringBuilder log;
        private volatile Uri uri;

        private volatile FFMpeg ffmpeg;
        private volatile bool removed = false,
                              stopped = false;

        private static readonly int _allowedTimeout = 2;
        private static readonly int _allowedMaxHours = 4;
        
        private volatile int urlNotFoundCount = 0;
        
        private void LogProgress(LogType type, string message)
        {
            var logEntry = string.Format("{0}: {1}", type, message);
            LastUpdate = logEntry;
            log.AppendLine(logEntry);

            if (OnEvent != null)
                OnEvent(type, logEntry);
        }
        #endregion

        public LogHandler OnEvent;
        public ChaturbateModel Model { get; set; }
        public StreamStatus Status { get; set; }
        public int ActivePart { get; private set; }
        public string Log { get { return log.ToString();  } }
        public string LastUpdate { get; set; }
        public bool IsWorking
        {
            get
            {
                return Status == StreamStatus.Active;
            }
        }
        public bool IsNotWorking
        {
            get
            {
                return !IsWorking;
            }
        }

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
            totalStreamTime = Stopwatch.StartNew();

            Task.Run(async () => 
            {
                uri = ChaturbateProxy.GetStreamLink(Model);

                for (;;)
                {
                    try
                    {
                        if (uri == null)
                        {
                            LogProgress(LogType.Warning, string.Format("{0} is offline, cannot download stream right now.", Model.StreamName));
                            return;
                        }

                        if (urlNotFoundCount > 200 || totalStreamTime.Elapsed.TotalHours > _allowedMaxHours)
                            await Stop();

                        if (removed || Status == StreamStatus.Idle)
                        {
                            if (!removed)
                                LogProgress(LogType.Update, string.Format("Show recored succesfully for {0}.", Model.StreamName));

                            foreach (string clip in clipParts)
                                File.Delete(clip);

                            LogProgress(LogType.Success, string.Format("Temporary files cleared succesfully for {0}.", Model.StreamName));

                            if (removed)
                                Status = StreamStatus.Removed;

                            break;
                        }

                        // if the stream is active
                        if (uri.IsAvailable() && !stopped && !removed)
                        {
                            Status = StreamStatus.Active;
                            LogProgress(LogType.Update, string.Format("Starting to download {0} part {1}...", Model.StreamName, ActivePart));

                            // download the stream
                            string downloadPath = string.Format(@"{0}\{1}_part_{2}.mp4", outputPath, Model.StreamName, ActivePart++);
                            try
                            {
                                ffmpeg.SaveM3U8Stream(uri, new FileInfo(downloadPath));

                                // check if the file exists after the recording is finished
                                if (File.Exists(downloadPath))
                                    clipParts.Add(downloadPath);
                                else ActivePart--;
                            }
                            catch (FFMpegException fe)
                            {
                                if (fe.Message.ToLower().Contains("404 not found"))
                                {
                                    uri = ChaturbateProxy.GetStreamLink(Model);
                                    urlNotFoundCount++;
                                    ActivePart--;
                                }
                            }
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
                                string finalClipName = string.Format("{0}_recorded_on_{1}_{2}.mp4", Model.StreamName, DateTime.Now.ToString("MM_dd_yyyy"), DateTime.Now.Ticks);
                                string finalOutputPath = string.Format(@"{0}\{1}", outputPath, finalClipName);

                                Status = StreamStatus.Joining;

                                LogProgress(LogType.Update, string.Format("Joining {0} temporary parts for {1}...", clipParts.Count, Model.StreamName));
                                // join only good video stream parts
                                ffmpeg.Join(new FileInfo(finalOutputPath), goodParts);

                                if (File.Exists(finalOutputPath))
                                {
                                    string modelDirectoy = string.Format(@"{0}\{1}", outputPath, Model.StreamName);

                                    if(!Directory.Exists(modelDirectoy))
                                    {
                                        Directory.CreateDirectory(modelDirectoy);
                                    }

                                    File.Move(finalOutputPath, string.Format(@"{0}\{1}", modelDirectoy, finalClipName));

                                    Status = StreamStatus.Idle;
                                }
                                else
                                {
                                    Status = StreamStatus.IdleNoJoin;
                                    LogProgress(LogType.Warning, string.Format("File parts could not be joined for stream {0}.", Model.StreamName));
                                    break;
                                }
                            }
                        }
                    }                   
                    catch (Exception e)
                    {
                        LogProgress(LogType.Error, string.Format("Something happened while downloading the stream. Info: {0}", e.Message));
                    }
                }
            });
        }

        private async Task<bool> WaitForStatus(StreamStatus status)
        {
            var result = await Task.Run(() => {
                int tries = 0;
                // wait for five minutes before deciding to let go
                while (Status != status && tries++ < 150)
                {
                    Thread.Sleep(2000);
                }

                return Status == status;
            });

            return result;
        }

        public async Task<bool> Stop()
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

            return await WaitForStatus(StreamStatus.Idle);
        }

        public async Task<bool> Delete()
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

            return await WaitForStatus(StreamStatus.Removed);
        }

        public void Dispose()
        {
            ffmpeg.Dispose();
        }
    }
}
