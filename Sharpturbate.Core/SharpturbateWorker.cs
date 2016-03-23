using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FFMpegSharp;
using FFMpegSharp.FFMPEG;
using FFMpegSharp.FFMPEG.Exceptions;
using Sharpturbate.Core.Aspects;
using Sharpturbate.Core.Aspects.Parsers;
using Sharpturbate.Core.Browser;
using Sharpturbate.Core.Enums;
using Sharpturbate.Core.Extensions;
using Sharpturbate.Core.Models;

namespace Sharpturbate.Core
{
    public delegate void LogHandler(LogType type, string message);

    public sealed class SharpturbateWorker : IDisposable
    {
        #region Constructor and Properties

        public LogHandler OnEvent;

        public SharpturbateWorker(ChaturbateModel model)
        {
            Model = model;
            clipParts = new List<string>();
            ffmpeg = new FFMpeg();
            log = new StringBuilder();
        }

        public ChaturbateModel Model { get; set; }
        public StreamStatus Status { get; set; }
        public int ActivePart { get; private set; }

        public string Log => log.ToString();

        public string LastUpdate { get; set; }

        public bool IsWorking => Status == StreamStatus.Active;

        public bool IsNotWorking => !IsWorking;

        public bool IsComplete { get { return Status == StreamStatus.Idle; } }

        public void Dispose()
        {
            ffmpeg.Dispose();
        }

        #endregion

        public void Start(string outputPath, bool moveToFolder = false)
        {
            timeoutWatch = default(Stopwatch);
            streamWatch = Stopwatch.StartNew();

            Task.Run(() =>
            {
                uri = ChaturbateProxy.GetStreamLink(Model);

                while (Status != StreamStatus.Idle && Status != StreamStatus.IdleNoJoin && Status != StreamStatus.Removed)
                {
                    Thread.Sleep(200);
                    try
                    {
                        if (removed)
                            continue;

                        var timeOutExceeded = timeoutWatch?.Elapsed.TotalMinutes > AllowedTimeoutInMinutes ||
                                              streamWatch.Elapsed.TotalHours > AllowedMaxHours;
                        if (timeOutExceeded)
                            Stop();

                        var urlNeedsRefrsh = uri == default(Uri) && !stopped;
                        if (urlNeedsRefrsh)
                        {
                            uri = ChaturbateProxy.GetStreamLink(Model);
                            FlagTimeout();
                            if (uri == default(Uri))
                                continue;
                        }

                        var canDownlod = uri.IsAvailable() && !stopped;
                        if (canDownlod)
                        {
                            Status = StreamStatus.Active;

                            timeoutWatch = default(Stopwatch);

                            var downloadPath = $@"{outputPath}\{Model.StreamName}_part_{ActivePart++}.mp4";

                            DownloadStream(downloadPath);
                        }
                        else
                        {
                            FlagTimeout();

                            // check if it has timed out for long enough or if the process was stopped
                            if (!stopped) continue;

                            var finalClipName =
                                $"{Model.StreamName}_recorded_on_{DateTime.Now.ToString("MM_dd_yyyy")}_{DateTime.Now.Ticks}.mp4";
                            var finalOutputPath = $@"{outputPath}\{finalClipName}";

                            JoinPartialDownloads(finalOutputPath);

                            if (File.Exists(finalOutputPath))
                            {
                                if (moveToFolder)
                                {
                                    var modelDirectoy = $@"{outputPath}\{Model.StreamName}";

                                    if (!Directory.Exists(modelDirectoy))
                                    {
                                        Directory.CreateDirectory(modelDirectoy);
                                    }

                                    File.Move(finalOutputPath, $@"{modelDirectoy}\{finalClipName}");
                                }

                                RemoveTemporaryFiles();

                                Status = StreamStatus.Idle;
                            }
                            else
                            {
                                Status = StreamStatus.IdleNoJoin;
                                LogProgress(LogType.Warning,
                                    $"Could not join parts not be join for '{Model.StreamName}'.");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        LogProgress(LogType.Error,
                            $"Error downloading the stream. Info: {e.Message}");

                        if (LoggedExceptions.FirstOrDefault(x => string.Compare(x.Message, e.Message, true) == 0) == null)
                        {
                            ErrorParser.ExceptionInfo(e);
                            LoggedExceptions.Add(e);
                        }
                        else exceptionDuplicates++;

                        if (exceptionDuplicates > 50)
                        {
                            Status = StreamStatus.IdleNoJoin;
                            LogProgress(LogType.Error, "Stream recording has stopped due to errors. Partial downloads have not been deleted.");
                        }
                    }
                }
            });
        }

        private void Stop(bool log = true)
        {
            stopped = true;
            ffmpeg.Stop();   
                    
            if(log) 
                LogProgress(LogType.Update, "Downloaded parts are queued to be joined.");
        }

        public async Task<bool> StopAsync()
        {
            await Task.Run(() => 
            {
                int tries = 0;
                LogProgress(LogType.Update, "Attempting to stop download.");
                while (ffmpeg.IsWorking && tries++ < 10)
                {
                    Stop(tries == 0);
                    Thread.Sleep(300);
                }
            });

            return await WaitForStatus(StreamStatus.Idle);
        }

        public async Task<bool> DeleteAsync()
        {
            removed = true;
            ffmpeg.Kill();

            if (ffmpeg.IsKillFaulty)
            {
                LogProgress(LogType.Error, "The FFMpeg process suffered a faulty kill.");
            }
            else
            {
                LogProgress(LogType.Update, "Downloaded parts are queued for removal.");
            }

            return await Task.Run(() =>
            {
                Status = StreamStatus.Removed;
                return RemoveTemporaryFiles();
            });
        }

        #region Private Methods

        private void FlagTimeout()
        {
            if (Status == StreamStatus.TimeOut) return;

            timeoutWatch = Stopwatch.StartNew();
            LogProgress(LogType.Warning,
                $"Stream timed out for after recording part {ActivePart}...");
            Status = StreamStatus.TimeOut;
        }

        [LogData]
        private void DownloadStream(string downloadPath)
        {
            try
            {
                LogProgress(LogType.Update,
                    $"Starting to download {Model.StreamName} part {ActivePart}...");

                ffmpeg.SaveM3U8Stream(uri, new FileInfo(downloadPath));

                // check if the file exists after the recording is finished
                if (File.Exists(downloadPath))
                    clipParts.Add(downloadPath);
                else ActivePart--;
            }
            catch (FFMpegException fe)
            {
                if (!fe.Message.ToLower().Contains("404 not found")) return;

                uri = default(Uri);
                LogProgress(LogType.Warning, $"{Model.StreamName} is offline, cannot download stream right now.");
                ActivePart--;
            }
        }

        [LogData]
        private void JoinPartialDownloads(string finalOutputPath)
        {
            if (Status == StreamStatus.Joining) return;

            Status = StreamStatus.Joining;
            var joinedParts = GoodParts;

            if (joinedParts.Length == 0)
            {
                LogProgress(LogType.Warning, "No good temp files available. Join and capture aborted.");
                return;
            }

            LogProgress(LogType.Update,
                $"Joining {joinedParts.Length} temporary parts for {Model.StreamName}...");
            // join only good video stream parts
            ffmpeg.Join(new FileInfo(finalOutputPath), joinedParts);
        }

        private bool RemoveTemporaryFiles()
        {
            try
            {
                if (!removed)
                    LogProgress(LogType.Update,
                        $"Show recored succesfully for {Model.StreamName}.");

                foreach (var clip in clipParts)
                    File.Delete(clip);

                LogProgress(LogType.Success,
                    $"Temporary files cleared succesfully for {Model.StreamName}.");
            }
            catch
            {
                LogProgress(LogType.Error,
                    $"Could not clear temporary files for {Model.StreamName}.");
                return false;
            }
            return true;
        }

        private async Task<bool> WaitForStatus(StreamStatus status)
        {
            var result = await Task.Run(() =>
            {
                var tries = 0;
                // wait for five minutes before deciding to let go
                while (Status != status && tries++ < 150)
                {
                    Thread.Sleep(100);
                }

                return Status == status;
            });

            return result;
        }

        private void LogProgress(LogType type, string message)
        {
            var logEntry = $"{type}: {message}";

            LastUpdate = logEntry;

            log.AppendLine(logEntry);

            OnEvent?.Invoke(type, logEntry);
        }

        #endregion

        #region Private Properties

        private const int AllowedTimeoutInMinutes = 4;
        private const int AllowedMaxHours = 3;
        private List<Exception> LoggedExceptions { get; } = new List<Exception>();

        private IList<string> clipParts;        
        private VideoInfo[] GoodParts
        {
            get
            {
                IList<VideoInfo> parts = new List<VideoInfo>();
                var corruptParts = 0;
                foreach (var clip in clipParts)
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

                if (corruptParts > 0)
                    LogProgress(LogType.Warning,
                        $"Removing {corruptParts} parts out of {clipParts.Count}, because of corruption. Joining only healthy video parts.");

                return parts.ToArray();
            }
        }
        private volatile Stopwatch streamWatch,
                                   timeoutWatch;
        private StringBuilder log;
        private Uri uri;

        private volatile FFMpeg ffmpeg;
        private volatile bool removed,
                              stopped;

        private int exceptionDuplicates = 0;

        #endregion
    }
}