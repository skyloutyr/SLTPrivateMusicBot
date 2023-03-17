namespace SLTPrivateMusicBot.Player
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;

    public static class YoutubeDL
    {
        public static string TempDirectory { get; set; }
        public static List<string> TempFiles { get; } = new List<string>();

        public static long GetExpirationDate(string url)
        {
            if (url.IndexOf("?expire=") != -1)
            {
                int expireInd = url.IndexOf("?expire=") + 8;
                int nextInd = url.IndexOf('&', expireInd);
                string timestamp = url.Substring(expireInd, nextInd - expireInd);
                if (long.TryParse(timestamp, out long l))
                {
                    return l;
                }
            }

            return 0;
        }

        public static string GetVideoTitle(Uri uri)
        {
            if (uri.Scheme == Uri.UriSchemeHttps && uri.Host.Equals("www.youtube.com")) // Can download from yt
            {
                Process p = Process.Start(new ProcessStartInfo
                {
                    FileName = "./yt-dlp.exe",
                    Arguments = $"--restrict-filenames --no-playlist --no-check-certificate -e -s --skip-download -- \"{ uri.AbsoluteUri }\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                });

                if (p == null)
                {
                    throw new Exception("Process is null, youtube-dl likely not found!");
                }

                p.WaitForExit();
                return p.StandardOutput.ReadToEnd();
            }

            return string.Empty;
        }

        public static string GetVideoStreamUrl(Uri uri)
        {
            if (uri.Scheme == Uri.UriSchemeHttps && uri.Host.Equals("www.youtube.com")) // Can download from yt
            {
                Process p = Process.Start(new ProcessStartInfo
                {
                    FileName = "./yt-dlp.exe",
                    Arguments = $"-vn --format \"bestaudio\" --restrict-filenames --no-playlist --no-check-certificate -g -s --skip-download -- \"{ uri.AbsoluteUri }\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                });

                if (p == null)
                {
                    throw new Exception("Process is null, youtube-dl likely not found!");
                }

                p.WaitForExit();
                return p.StandardOutput.ReadToEnd();
            }

            return string.Empty;
        }

        public static bool DownloadVideoTo(Uri uri, string loc)
        {
            try
            {
                if (uri.Scheme == Uri.UriSchemeHttps && uri.Host.Equals("www.youtube.com")) // Can download from yt
                {
                    Process p = Process.Start(new ProcessStartInfo
                    {
                        FileName = "./yt-dlp.exe",
                        Arguments = $"--no-playlist -x --audio-format mp3 -o \"{loc}\" -- \"{uri.AbsoluteUri}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    });

                    if (p == null)
                    {
                        throw new Exception("Process is null, youtube-dl likely not found!");
                    }

                    p.WaitForExit();
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public static string DownloadVideo(Uri uri)
        {
            CheckTempDir();
            string newTempDirectory = Path.Combine(TempDirectory, Guid.NewGuid().ToString());
            Directory.CreateDirectory(newTempDirectory);
            try
            {
                if (uri.Scheme == Uri.UriSchemeHttps && uri.Host.Equals("www.youtube.com")) // Can download from yt
                {
                    Process p = Process.Start(new ProcessStartInfo
                    {
                        FileName = "./yt-dlp.exe",
                        Arguments = $"--no-playlist -x --audio-format mp3 -o { newTempDirectory }\\%(title)s.%(ext)s -- \"{ uri.AbsoluteUri }\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    });

                    if (p == null)
                    {
                        throw new Exception("Process is null, youtube-dl likely not found!");
                    }

                    p.WaitForExit();
                    if (Directory.GetFiles(newTempDirectory).Length == 1)
                    {
                        string file = Directory.GetFiles(newTempDirectory)[0];
                        File.Move(file, Path.Combine(TempDirectory, Path.GetFileName(file)));
                        file = Path.Combine(TempDirectory, Path.GetFileName(file));
                        TempFiles.Add(file);
                        return file;
                    }
                }
            }
            finally
            {
                Directory.Delete(newTempDirectory, true);
            }

            throw new ArgumentException("URI does not point to youtube!");
        }

        public static void CheckTempDir()
        {
            if (string.IsNullOrEmpty(TempDirectory))
            {
                TempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(TempDirectory);
            }
        }

        public static void Cleanup()
        {
            if (!string.IsNullOrEmpty(TempDirectory))
            {
                Directory.Delete(TempDirectory, true);
            }
        }
    }
}
