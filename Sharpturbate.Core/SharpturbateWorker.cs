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

        public void Dispose()
        {
            _ffmpeg.Dispose();
        }
        #endregion

        public void Start(string outputPath)
        {
            _timeoutWatch = default(Stopwatch);
            _totalStreamTime = Stopwatch.StartNew();

            Task.Run(() =>
            {
                _uri = ChaturbateProxy.GetStreamLink(Model);

                for (;;)
                {
                    try
                    {
                        if (_timeoutWatch?.Elapsed.TotalMinutes > AllowedTimeout ||
                            _totalStreamTime.Elapsed.TotalHours > AllowedMaxHours)
                                Stop();

                        if (_uri == null && !_stopped)
                        {
                            _uri = ChaturbateProxy.GetStreamLink(Model);
                            SetTimeout();
                            Thread.Sleep(2000);
                            continue;
                        }

                        if (_removed || Status == StreamStatus.Idle)
                        {
                            RemoveTempFiles();
                            break;
                        }

                        // if the stream is active
                        if (_uri.IsAvailable() && !_stopped && !_removed)
                        {
                            Download(outputPath);
                        }
                        else
                        {
                            SetTimeout();

                            // check if it has timed out for long enough or if the process was stopped
                            if (!_stopped) continue;

                            var finalClipName =
                                $"{Model.StreamName}_recorded_on_{DateTime.Now.ToString("MM_dd_yyyy")}_{DateTime.Now.Ticks}.mp4";
                            var finalOutputPath = $@"{outputPath}\{finalClipName}";

                            JoinParts(finalOutputPath);

                            if (File.Exists(finalOutputPath))
                            {
                                var modelDirectoy = $@"{outputPath}\{Model.StreamName}";

                                if (!Directory.Exists(modelDirectoy))
                                {
                                    Directory.CreateDirectory(modelDirectoy);
                                }

                                File.Move(finalOutputPath, $@"{modelDirectoy}\{finalClipName}");

                                Status = StreamStatus.Idle;
                            }
                            else
                            {
                                Status = StreamStatus.IdleNoJoin;
                                LogProgress(LogType.Warning,
                                    $"File parts could not be joined for stream {Model.StreamName}.");
                                break;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        LogProgress(LogType.Error,
                            $"Something happened while downloading the stream. Info: {e.Message}");
                    }
                }
            });
        }

        public async Task<bool> Stop()
        {
            try
            {
                _stopped = true;
                _ffmpeg.Stop();
                LogProgress(LogType.Update, "Downloaded parts are queued to be joined.");
            }
            catch (Exception e)
            {
                LogProgress(LogType.Error,
                    $"An error occured while stopping the stream. Details: {e.Message}");
            }

            return await WaitForStatus(StreamStatus.Idle);
        }

        public async Task<bool> Delete()
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

            return await WaitForStatus(StreamStatus.Removed);
        }

        #region Private Methods
        private void SetTimeout()
        {
            if (Status == StreamStatus.TimeOut) return;

            _timeoutWatch = Stopwatch.StartNew();
            LogProgress(LogType.Warning,
                $"Stream timed out for after recording part {ActivePart - 1}...");
            Status = StreamStatus.TimeOut;
        }

        private void Download(string outputPath)
        {
            Status = StreamStatus.Active;
            LogProgress(LogType.Update,
                $"Starting to download {Model.StreamName} part {ActivePart}...");

            // download the stream
            var downloadPath = $@"{outputPath}\{Model.StreamName}_part_{ActivePart++}.mp4";
            try
            {
                _ffmpeg.SaveM3U8Stream(_uri, new FileInfo(downloadPath));

                // check if the file exists after the recording is finished
                if (File.Exists(downloadPath))
                    _clipParts.Add(downloadPath);
                else ActivePart--;
            }
            catch (FFMpegException fe)
            {
                if (!fe.Message.ToLower().Contains("404 not found")) return;
                
                _uri = null;
                LogProgress(LogType.Warning, $"{Model.StreamName} is offline, cannot download stream right now.");
                ActivePart--;
            }
        }

        private void JoinParts(string finalOutputPath)
        {
            Status = StreamStatus.Joining;

            LogProgress(LogType.Update,
                $"Joining {_clipParts.Count} temporary parts for {Model.StreamName}...");
            // join only good video stream parts
            _ffmpeg.Join(new FileInfo(finalOutputPath), GoodParts);
        }

        private void RemoveTempFiles()
        {
            if (!_removed)
                LogProgress(LogType.Update,
                    $"Show recored succesfully for {Model.StreamName}.");

            foreach (var clip in _clipParts)
                File.Delete(clip);

            LogProgress(LogType.Success,
                $"Temporary files cleared succesfully for {Model.StreamName}.");

            if (_removed)
                Status = StreamStatus.Removed;
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

        private volatile Stopwatch _totalStreamTime,
                                    _timeoutWatch;
        private volatile StringBuilder _log;
        private volatile Uri _uri;

        private volatile FFMpeg _ffmpeg;

        private volatile bool _removed,
            _stopped;

        private const int AllowedTimeout = 2;
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