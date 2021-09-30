namespace SLTPrivateMusicBot.Player
{
    using Newtonsoft.Json;
    using System;
    using System.Diagnostics;
    using System.IO;

    public static class FFMpeg
    {
        public static AudioMetadata GetAudioMetadata(string fPath)
        {
            Process process = Process.Start(new ProcessStartInfo
            {
                FileName = "ffprobe",
                Arguments = $"-hide_banner -print_format json -show_format \"{fPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });

            if (process == null)
            {
                throw new Exception("Process could not start!");
            }

            string s = process.StandardOutput.ReadToEnd();
            AudioMetadata af = default;
            try
            {
                af = JsonConvert.DeserializeObject<AudioMetadata>(s);
            }
            finally
            {
                process.Dispose();
            }

            return af;
        }

        public static StreamBackend StreamAudio(string fPath, int sampleRate)
        {
            Process process = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -i \"{fPath}\" -ac 2 -f s16le -ar {sampleRate} -blocksize 192000 -flush_packets 1 pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });

            if (process == null)
            {
                throw new Exception("Process could not start!");
            }

            return new StreamBackend(process);
        }

        public static byte[] GetAudio(string fPath, int sampleRate)
        {
            Process process = Process.Start(new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-hide_banner -loglevel panic -i \"{fPath}\" -ac 2 -f s16le -ar {sampleRate} pipe:1",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            });

            if (process == null)
            {
                throw new Exception("Process could not start!");
            }

            byte[] data;
            try
            {
                using Stream s = process.StandardOutput.BaseStream;
                using MemoryStream ms = new();
                s.CopyTo(ms);
                data = ms.ToArray();
            }
            finally
            {
                process.Dispose();
            }

            return data;
        }
    }

    public struct StreamBackend
    {
        private Process _proc;
        public StreamBackend(Process proc) => this._proc = proc;

        public Stream BaseOut => this._proc.StandardOutput.BaseStream;
        public Stream BaseErr => this._proc.StandardError.BaseStream;

        public bool CanRead()
        {
            try
            {
                return this.BaseOut.CanRead;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            this._proc.Dispose();
        }
    }
}
