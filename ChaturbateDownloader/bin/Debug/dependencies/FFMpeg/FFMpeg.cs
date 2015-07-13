using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace VideoTools
{
    public enum Speed
    {
        VerySlow,
        Slower,
        Slow,
        Medium,
        Fast,
        Faster,
        VeryFast,
        SuperFast,
        UltraFast
    }

    public enum VideoSize
    {
        HD,
        FullHD,
        ED,
        LD,
        Original
    }

    public class FFProbe
    {
        private string _ffprobePath;

        public FFProbe(string rootPath)
        {
            _ffprobePath = rootPath + "ffprobe.exe";
        }

        #region InternalHelpers
        private string _RunProcess(string args)
        {
            Process process = new Process();
            process.StartInfo.FileName = _ffprobePath;
            process.StartInfo.Arguments = args;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            string output = null;
            try
            {
                process.Start();
                output = process.StandardOutput.ReadToEnd();
            }
            catch (Exception)
            {
                output = "";
            }
            finally
            {
                process.WaitForExit();
                process.Close();
                GC.Collect();
            }
            return output;
        }

        private int GCD(int first, int second)
        {
            while (first != 0 && second != 0)
            {
                if (first > second)
                    first -= second;
                else second -= first;
            }
            return first == 0 ? second : first;
        }
        #endregion

        public VideoInfo GetVideoInfo(VideoInfo video)
        {
            string jsonOutput = _RunProcess("-v quiet -print_format json -show_streams " + video.Path);

           // Console.WriteLine(jsonOutput);

            var dict = (new JavaScriptSerializer()).Deserialize<Dictionary<string, dynamic>>(jsonOutput);
            int vid = dict["streams"][0]["codec_type"] == "video" ? 0 : 1, 
                aud = 1 - vid;
            
            // Get video duration
            double numberData;
            numberData = double.Parse(dict["streams"][vid]["duration"]);
            video.Duration = TimeSpan.FromSeconds(numberData);
            video.Duration = video.Duration.Subtract(TimeSpan.FromMilliseconds(video.Duration.Milliseconds));

            // Get audio format
            video.AudioFormat = dict["streams"][aud]["codec_name"];

            // Get video format
            video.VideoFormat = dict["streams"][vid]["codec_name"];

            // Get video width
            video.Width = dict["streams"][vid]["width"];

            // Get video height
            video.Height = dict["streams"][vid]["height"];

            // Get video size in megabytes
            double videoSize = double.Parse(dict["streams"][vid]["bit_rate"]) * numberData / 8388608,
                audioSize = double.Parse(dict["streams"][aud]["bit_rate"]) * double.Parse(dict["streams"][aud]["duration"]) / 8388608;
            video.Size = Math.Round(videoSize + audioSize, 2);
            
            // Get video aspect ratio
            int cd = GCD(video.Width, video.Height);
            video.Ratio = video.Width / cd + ":" + video.Height / cd;

            // Get video framerate
            string[] fr = ((string)dict["streams"][vid]["r_frame_rate"]).Split('/');
            video.FrameRate = Math.Round(double.Parse(fr[0]) / double.Parse(fr[1]), 3);

            // Update info flat
            video.IsInfoUpdated = true;

            GC.Collect();

            return video;
        }

        public void SetVideoInfo(ref VideoInfo video)
        {
            video = GetVideoInfo(video);
        }
    }

    public class FFMpeg
    {
        private string _ffmpegPath;
        private string _outputPath;
        TimeSpan? totalVideoTime = null;
        private int? _processId = null;

        #region InternalHelpers
        private string _GetScale(VideoSize size)
        {
            string scale = " -vf scale=";

            switch (size.ToString())
            {
                case "FullHD": scale += "-1:1080"; break;
                case "HD": scale += "-1:720"; break;
                case "EDTV": scale += "-1: 480"; break;
                case "LD": scale += "-1:360"; break;
                default: scale = ""; break;
            }

            return scale;
        }

        private bool _RunProcess(string arguments, bool redirectOutput = false)
        {
            bool SuccessState = true;
            Process process = new Process();
            process.StartInfo.FileName = _ffmpegPath;
            process.StartInfo.Arguments = arguments;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardError = redirectOutput;
            try
            {
                process.Start();
                _processId = process.Id;
                if (redirectOutput)
                {
                    process.ErrorDataReceived += OutputData;
                    process.BeginErrorReadLine();
                }
            }
            catch (Exception)
            {
                SuccessState = false;
            }
            finally
            {
                process.WaitForExit();
                process.Close();
                GC.Collect();
            }
            return SuccessState;
        }

        private void _ConversionExceptionCheck(VideoInfo originalVideo, string convertedPath)
        {
            if (File.Exists(convertedPath))
                throw new Exception("The output file already exists !");

            if (!File.Exists(originalVideo.Path))
                throw new Exception("Input file does not exist !");
        }

        private void OutputData(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                Regex r = new Regex(@"\w\w:\w\w:\w\w");
                Match m = r.Match(e.Data);
                if (_outputPath != null && e.Data.Contains("frame"))
                {
                    if (m.Success)
                    {
                        TimeSpan t = TimeSpan.Parse(m.Value);
                        int percentage = (int)(t.TotalSeconds / totalVideoTime.Value.TotalSeconds * 100);
                        StreamWriter data = new StreamWriter(_outputPath, false);
                        data.WriteLine(percentage);
                        data.Close();
                    }
                }
            }
            Console.WriteLine(e.Data);
        }
        #endregion

        public FFMpeg(string rootPath, string conversionPercentageOutputPath = null)
        {
            _ffmpegPath = rootPath + "ffmpeg.exe";
            _outputPath = conversionPercentageOutputPath;
        }

        #region Thumbnails
        public bool SaveThumbnail(VideoInfo video, string thumbPath, TimeSpan? thumbTime = null, int thumbWidth = 300, int thumbHeight = 169)
        {
            if (thumbTime == null)
                thumbTime = new TimeSpan(0, 0, 7);
            if (File.Exists(thumbPath))
                throw new Exception("Thumbnail already exists !");

            if (!File.Exists(video.Path))
                throw new Exception("Input file does not exist !");

            string thumbArgs,
                   thumbSize = thumbWidth.ToString() + "x" + thumbHeight.ToString();

            thumbArgs = "-i " + video.Path + " -vcodec png -vframes 1 -ss " + thumbTime.ToString() +" -s " + thumbSize + " " + thumbPath;

            return _RunProcess(thumbArgs);
        }

        public bool SaveThumbnail(string videoPath, string thumbPath, TimeSpan? thumbTime = null, int thumbWidth = 300, int thumbHeight = 169)
        {
            return SaveThumbnail(new VideoInfo(videoPath), thumbPath, thumbTime, thumbWidth, thumbHeight);
        }
        #endregion

        #region Convert
        public bool ToMP4(VideoInfo originalVideo, string convertedPath, Speed speed = Speed.SuperFast, VideoSize size = VideoSize.Original, bool multithread = false)
        {
            totalVideoTime = originalVideo.Duration;
            string threadCount = multithread ? Environment.ProcessorCount.ToString() : "1",
                   scale = _GetScale(size);

            _ConversionExceptionCheck(originalVideo, convertedPath);

            if ((new FileInfo(convertedPath).Extension != ".mp4"))
                throw new Exception("Invalid output file extension .mp4 required.");

            string conversionArgs = " -i " + originalVideo.Path + " -threads " + threadCount + scale + " -b:v 2000k -vcodec libx264 -preset " + speed.ToString().ToLower() + " -g 30 " + convertedPath;

            return _RunProcess(conversionArgs, true);
        }

        public bool ToMP4(string originalPath, string convertedPath, Speed speed = Speed.SuperFast, VideoSize size = VideoSize.Original, bool multithread = false)
        {
            return ToMP4(new VideoInfo(originalPath), convertedPath, speed, size, multithread);
        }

        public bool ToWebM(VideoInfo originalVideo, string convertedPath, VideoSize size = VideoSize.Original, bool multithread = false)
        {
            string threadCount = multithread ? Environment.ProcessorCount.ToString() : "1",
                scale = _GetScale(size);

            _ConversionExceptionCheck(originalVideo, convertedPath);

            if ((new FileInfo(convertedPath).Extension != ".webm"))
                throw new Exception("Invalid output file extension .webm required.");

            string conversionArgs = " -i " + originalVideo.Path + " -threads " + threadCount + scale + " -vcodec libvpx -quality good -cpu-used 0 -b:v 1500k -qmin 10 -qmax 42 -maxrate 500k -bufsize 1000k -codec:a libvorbis -b:a 128k " + convertedPath;

            return _RunProcess(conversionArgs, true);
        }

        public bool ToWebM(string originalPath, string convertedPath, VideoSize size = VideoSize.Original, bool multithread = false)
        {
            return ToWebM(new VideoInfo(originalPath), convertedPath, size, multithread);
        }

        public bool ToOGV(VideoInfo originalVideo, string convertedPath, VideoSize size = VideoSize.Original, bool multithread = false)
        {
            string threadCount = multithread ? Environment.ProcessorCount.ToString() : "1",
                scale = _GetScale(size);

            _ConversionExceptionCheck(originalVideo, convertedPath);

            if((new FileInfo(convertedPath).Extension != ".ogv"))
                throw new Exception("Invalid output file extension .ogv required.");

            string conversionArgs = " -i " + originalVideo.Path + " -threads " + threadCount + scale + " -codec:v libtheora -qscale:v 7 -codec:a libvorbis -qscale:a 5 " + convertedPath;

            return _RunProcess(conversionArgs, true);
        }

        public bool ToOGV(string originalPath, string convertedPath, VideoSize size = VideoSize.Original, bool multithread = false)
        {
            return ToOGV(new VideoInfo(originalPath), convertedPath, size, true);
        }
        #endregion

        #region Other
        public bool Join(VideoInfo one, VideoInfo two, string convertedPath, Speed speed = Speed.SuperFast, VideoSize size = VideoSize.Original, bool multithread = false)
        {
            bool SuccessState = true;
            if ((new FileInfo(convertedPath).Extension != ".mp4"))
                throw new Exception("Invalid output file extension .mp4 required.");

            string tempOnePath = one.RootDirectory + one.FileName + "part1.ts", 
                   tempTwoPath = two.RootDirectory + two.FileName + "part2.ts",
                   joinedTemp = one.RootDirectory + one.FileName + two.FileName + "joined.ts";

            string conversionArgs = " -i " + one.Path + " -c copy -bsf:v h264_mp4toannexb -f mpegts " + tempOnePath;

            SuccessState = SuccessState && _RunProcess(conversionArgs);

            conversionArgs = " -i " + two.Path + " -c copy -bsf:v h264_mp4toannexb -f mpegts " + tempTwoPath;

            SuccessState = SuccessState && _RunProcess(conversionArgs);

            conversionArgs = " -f mpegts -i \"concat:" + tempOnePath + "|" + tempTwoPath + "\" -c copy -bsf:v h264_mp4toannexb -f mpegts " + joinedTemp;

            SuccessState = SuccessState && _RunProcess(conversionArgs);

            File.Delete(tempOnePath);
            File.Delete(tempTwoPath);

            SuccessState = SuccessState && ToMP4(joinedTemp, convertedPath, speed, size, multithread);

            File.Delete(joinedTemp);

            GC.Collect();

            return SuccessState;
        }
        #endregion
    }

    public class VideoInfo
    {
        private FileInfo _File;
        public bool IsInfoUpdated = false;
        public TimeSpan Duration { get; set; }
        public string AudioFormat { get; set; }
        public string VideoFormat { get; set; }
        public string Ratio { get; set; }
        public double FrameRate { get; set; }
        public int Height { get; set; }
        public int Width { get; set; }
        public double Size { get; set; }

        public VideoInfo(string path)
        {
            if (!File.Exists(path))
                throw new Exception("Video file does not exist.");
            _File = new FileInfo(path);
        }

        public string GetInfo()
        {
            return IsInfoUpdated ?
                   "Video Path : " + Path + "\n" +
                   "Video Root : " + RootDirectory + "\n" +
                   "Video Name: " + FileName + "\n" +
                   "Video Extension : " + Extension + "\n" +
                   "Video Duration : " + Duration + "\n" +
                   "Audio Format : " + AudioFormat + "\n" +
                   "Video Format : " + VideoFormat + "\n" +
                   "Aspect Ratio : " + Ratio + "\n" +
                   "Framerate : " + FrameRate + "fps\n" +
                   "Resolution : " + Width + "x" + Height + "\n" +
                   "Size : " + Size + " Mb" : "No video info gathered yet!\n";
        }

        public string RootDirectory { get { return _File.Directory.FullName + "\\"; } }

        public string FileName { get { return _File.Name; } }

        public string Path { get { return _File.FullName; } }

        public string Extension { get { return _File.Extension; } }

        public void Delete()
        {
            _File.Delete();
        }

    }
}