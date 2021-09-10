namespace SLTPrivateMusicBot.Player
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;

    public static class YoutubeDL
    {
        public static string TempDirectory { get; set; }
        public static List<string> TempFiles { get; } = new List<string>();

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
                        FileName = "youtube-dl",
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
