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
        public void Start(string outputPath, bool moveToFolder = false)
        {
            _timeoutWatch = default(Stopwatch);
            _streamWatch = Stopwatch.StartNew();

            Task.Run(() =>
            {
                _uri = ChaturbateProxy.GetStreamLink(Model);

                while (Status != StreamStatus.Idle && Status != StreamStatus.IdleNoJoin)
                {
                    Thread.Sleep(2000);
                    try
                    {
                        if (_removed)
                            continue;

                        var timeOutExceeded = _timeoutWatch?.Elapsed.TotalMinutes > AllowedTimeoutInMinutes ||
                                              _streamWatch.Elapsed.TotalHours > AllowedMaxHours;
                        if (timeOutExceeded)
                            Stop();

                        var urlNeedsRefrsh = _uri == default(Uri) && !_stopped;
                        if (urlNeedsRefrsh)
                        {
                            _uri = ChaturbateProxy.GetStreamLink(Model);
                            FlagTimeout();
                            if (_uri == default(Uri))
                                continue;
                        }

                        var canDownlod = _uri.IsAvailable() && !_stopped;
                        if (canDownlod)
                        {
                            Status = StreamStatus.Active;

                            _timeoutWatch = default(Stopwatch);

                            var downloadPath = $@"{outputPath}\{Model.StreamName}_part_{ActivePart++}.mp4";

                            DownloadStream(downloadPath);
                        }
                        else
                        {
                            FlagTimeout();

                            // check if it has timed out for long enough or if the process was stopped
                            if (!_stopped) continue;

                            var finalClipName =
                                $"{Model.StreamName}_recorded_on_{DateTime.Now.ToString("MM_dd_yyyy")}_{DateTime.Now.Ticks}.mp4";
                            var finalOutputPath = $@"{outputPath}\{finalClipName}";

                            Status = StreamStatus.Joining;

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
                                    $"Could not be join for '{Model.StreamName}'.");
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        LogProgress(LogType.Error,
                            $"Error downloading the stream. Info: {e.Message}");

                        if (loggedExceptions.FirstOrDefault(x => string.Compare(x.Message, e.Message, true) == 0) == null)
                        {
                            ErrorParser.ExceptionInfo(e);
                            loggedExceptions.Add(e);
                        }
                        else _exceptionDuplicates++;

                        if (_exceptionDuplicates > 50)
                        {
                            Status = StreamStatus.IdleNoJoin;
                            LogProgress(LogType.Error, "Stream recording has stopped due to multiple thrown errors. Partial downloads have not been deleted.");
                        }
                    }
                }
            });
        }

        private void Stop()
        {
            _stopped = true;
            _ffmpeg.Stop();
            LogProgress(LogType.Update, "Downloaded parts are queued to be joined.");
        }

        public async Task<bool> StopAsync()
        {
            try
            {
                Stop();
            }
            catch (Exception e)
            {
                LogProgress(LogType.Error,
                    $"An error occured while stopping the stream. Details: {e.Message}");
            }

            return await WaitForStatus(StreamStatus.Idle);
        }

        public async Task<bool> DeleteAsync()
        {
            _removed = true;
            _ffmpeg.Kill();

            if (_ffmpeg.IsKillFaulty)
            {
                LogProgress(LogType.Error, "The FFMpeg process suffered a faulty kill.");
            }
            else
            {
                LogProgress(LogType.Update, "Downloaded parts are queued for removal.");
            }

            return await Task.Run(() =>
            {
                return RemoveTemporaryFiles();
            });
        }

        #region Constructor and Properties

        public LogHandler OnEvent;

        public SharpturbateWorker(ChaturbateModel model)
        {
            Model = model;
            _clipParts = new List<string>();
            _ffmpeg = new FFMpeg();
            _log = new StringBuilder();
        }

        public ChaturbateModel Model { get; set; }
        public StreamStatus Status { get; set; }
        public int ActivePart { get; private set; }

        public string Log => _log.ToString();

        public string LastUpdate { get; set; }

        public bool IsWorking => Status == StreamStatus.Active;

        public bool IsNotWorking => !IsWorking;

        public bool IsComplete { get { return Status == StreamStatus.Idle; } }

        public void Dispose()
        {
            _ffmpeg.Dispose();
        }

        #endregion

        #region Private Methods

        private void FlagTimeout()
        {
            if (Status == StreamStatus.TimeOut) return;

            _timeoutWatch = Stopwatch.StartNew();
            LogProgress(LogType.Warning,
                $"Stream timed out for after recording part {ActivePart - 1}...");
            Status = StreamStatus.TimeOut;
        }

        [LogData]
        private void DownloadStream(string downloadPath)
        {
            try
            {
                LogProgress(LogType.Update,
                    $"Starting to download {Model.StreamName} part {ActivePart}...");

                _ffmpeg.SaveM3U8Stream(_uri, new FileInfo(downloadPath));

                // check if the file exists after the recording is finished
                if (File.Exists(downloadPath))
                    _clipParts.Add(downloadPath);
                else ActivePart--;
            }
            catch (FFMpegException fe)
            {
                if (!fe.Message.ToLower().Contains("404 not found")) return;

                _uri = default(Uri);
                LogProgress(LogType.Warning, $"{Model.StreamName} is offline, cannot download stream right now.");
                ActivePart--;
            }
        }

        [LogData]
        private void JoinPartialDownloads(string finalOutputPath)
        {
            if (Status == StreamStatus.Joining) return;

            var joinedParts = GoodParts;
            LogProgress(LogType.Update,
                $"Joining {joinedParts.Length} temporary parts for {Model.StreamName}...");
            // join only good video stream parts
            _ffmpeg.Join(new FileInfo(finalOutputPath), joinedParts);
        }

        private bool RemoveTemporaryFiles()
        {
            try
            {
                if (!_removed)
                    LogProgress(LogType.Update,
                        $"Show recored succesfully for {Model.StreamName}.");

                foreach (var clip in _clipParts)
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
                    Thread.Sleep(2000);
                }

                return Status == status;
            });

            return result;
        }

        #endregion

        #region Private Members

        private volatile IList<string> _clipParts;
        private List<Exception> loggedExceptions { get; set; } = new List<Exception>();  
        private VideoInfo[] GoodParts
        {
            get
            {
                IList<VideoInfo> parts = new List<VideoInfo>();
                var corruptParts = 0;
                foreach (var clip in _clipParts)
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
                        $"Removing {corruptParts} parts out of {_clipParts.Count}, because of corruption. Joining only healthy video parts.");

                return parts.ToArray();
            }
        }

        private volatile Stopwatch _streamWatch,
            _timeoutWatch;

        private volatile StringBuilder _log;
        private volatile Uri _uri;

        private volatile FFMpeg _ffmpeg;

        private volatile bool _removed,
            _stopped;

        private int _exceptionDuplicates = 0;
        private const int AllowedTimeoutInMinutes = 4;
        private const int AllowedMaxHours = 4;

        private void LogProgress(LogType type, string message)
        {
            var logEntry = $"{type}: {message}";

            LastUpdate = logEntry;

            _log.AppendLine(logEntry);

            OnEvent?.Invoke(type, logEntry);
        }

        #endregion
    }
}