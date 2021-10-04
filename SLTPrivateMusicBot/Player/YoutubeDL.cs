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
            //https://r2---sn-n3toxu-axqs.googlevideo.com/videoplayback?expire=1633207490&ei=YnBYYabwAs7IyAW1xK-ICw&ip=94.19.112.176&id=o-ANkvEG1ZUt3rPWD0o1LvqudWkoF9yXa-1kQFmSQi0dhy&itag=140&source=youtube&requiressl=yes&mh=Jc&mm=31%2C29&mn=sn-n3toxu-axqs%2Csn-5goeen7k&ms=au%2Crdu&mv=m&mvi=2&pl=20&initcwndbps=1641250&vprv=1&mime=audio%2Fmp4&ns=wn2jSvMBjc7S1WjnqsHBuxMG&gir=yes&clen=13913800&dur=859.649&lmt=1591309167145943&mt=1633185167&fvip=2&keepalive=yes&fexp=24001373%2C24007246&c=WEB&txp=5531432&n=h8ZK_Luot9K4TRXf&sparams=expire%2Cei%2Cip%2Cid%2Citag%2Csource%2Crequiressl%2Cvprv%2Cmime%2Cns%2Cgir%2Cclen%2Cdur%2Clmt&sig=AOq0QJ8wRgIhAPztgDdbzxx9gwaaMe_Le6WN-IT3wBY5aaG75ETHPYcxAiEAzB1mkBW2_S24HfXpzHydt6nC5Dr-kjqHTbESRqLFPIE%3D&lsparams=mh%2Cmm%2Cmn%2Cms%2Cmv%2Cmvi%2Cpl%2Cinitcwndbps&lsig=AG3C_xAwRQIgLkP3QtRtXj6UXctwNGIPZ6DCC89ipOxLAxaT5EXQieECIQD8psmRzIqabnL7UdsV7aAgcP78cqVmr3nCb2CYKMLdYA%3D%3D
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
                    FileName = "./youtube-dl.exe",
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
                    FileName = "./youtube-dl.exe",
                    Arguments = $"--format \"251/webm\" --restrict-filenames --no-playlist --no-check-certificate -g -s --skip-download -- \"{ uri.AbsoluteUri }\"",
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
                        FileName = "./youtube-dl.exe",
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
