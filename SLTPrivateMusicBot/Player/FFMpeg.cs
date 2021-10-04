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
            Process process = null;
            try
            {
                process = Process.Start(new ProcessStartInfo
                {
                    FileName = "./ffprobe.exe",
                    Arguments = $"-hide_banner -print_format json -show_format \"{fPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                });
            }
            catch (Exception e)
            {
                App.Log("[FATAL] Could not start ffprobe!", e);
                throw new Exception("Process could not start!");
            }

            if (process == null)
            {
                App.Log("[FATAL] Could not start ffprobe - process was null, but no exception?");
                throw new Exception("Process could not start!");
            }

            AudioMetadata af = default;
            try
            {
                string s = process.StandardOutput.ReadToEnd();
                App.Log("[FINE] Outputting audio metadata:");
                foreach (string st in s.Split('\n'))
                {
                    App.Log(st);
                }

                try
                {
                    af = JsonConvert.DeserializeObject<AudioMetadata>(s);
                }
                catch (Exception e)
                {
                    App.Log("[ERROR] Could not get ffprobe data!", e);
                }
                finally
                {
                    process.Dispose();
                }
            }
            catch (Exception stdoutreadex)
            {
                App.Log("[ERROR] Could not get ffprobe stream data!", stdoutreadex);
            }

            return af;
        }

        public static StreamBackend StreamAudio(string fPath, int sampleRate, int audioOffsetSeconds = 0)
        {
            Process process = Process.Start(new ProcessStartInfo
            {
                FileName = "./ffmpeg.exe",
                Arguments = $"-hide_banner -loglevel panic -i \"{fPath}\" -ac 2 -f s16le -ar {sampleRate} -blocksize {MainWindow.MusicRate} -flush_packets 1 {(audioOffsetSeconds == 0 ? "" : ("-ss " + audioOffsetSeconds + " "))}pipe:1",
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
            Process process = null;
            try 
            { 
                process = Process.Start(new ProcessStartInfo
                {
                    FileName = "./ffmpeg.exe",
                    Arguments = $"-hide_banner -loglevel panic -i \"{fPath}\" -ac 2 -f s16le -ar {sampleRate} pipe:1",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                });
            }
            catch (Exception e)
            {
                App.Log("[FATAL] Could not start ffmpeg!", e);
                throw new Exception("Process could not start!");
            }

            if (process == null)
            {
                App.Log("[FATAL] Could not start ffmpeg - process was null, but no exception?");
                throw new Exception("Process could not start!");
            }

            byte[] data = null;
            try
            {
                using Stream s = process.StandardOutput.BaseStream;
                using MemoryStream ms = new();
                s.CopyTo(ms);
                data = ms.ToArray();
            }
            catch (Exception e)
            {
                App.Log("[ERROR] error reading ffmpeg stream!", e);
            }
            finally
            {
                process.Dispose();
            }

            return data;
        }
    }

    public class StreamBackend
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
