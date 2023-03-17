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

            new ProcStdStreamObserver(process, process.StandardError, s => App.Log(s));
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
            // -lts_verify will crash ffmpeg if used for a local filepath for whatever reason
            // One might think that it would simply ignore the argument that doesn't make sense in the current context
            // Or maybe spew out a warning but continue regardless, ignoring it but giving a heads-up
            // But apparently for ffmpeg an argument like that is a case for a fatal error...
            string args = fPath.StartsWith("http:") || fPath.StartsWith("https:") ?
                $"-hide_banner -loglevel quiet -tls_verify 0 -analyzeduration 922337203685477580 -probesize 922337203685477580 -thread_queue_size 4096 -re -i \"{fPath}\" -ac 2 -f s16le -blocksize {MainWindow.MusicRate} -ar {sampleRate}  -flush_packets 1 {(audioOffsetSeconds == 0 ? "" : ("-ss " + audioOffsetSeconds + " "))}pipe:1" :
                $"-hide_banner -loglevel quiet -analyzeduration 922337203685477580 -probesize 922337203685477580 -thread_queue_size 4096 -re -i \"{fPath}\" -ac 2 -f s16le -blocksize {MainWindow.MusicRate} -ar {sampleRate}  -flush_packets 1 {(audioOffsetSeconds == 0 ? "" : ("-ss " + audioOffsetSeconds + " "))}pipe:1";
            Process process = Process.Start(new ProcessStartInfo
            {
                FileName = "./ffmpeg.exe",
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            });

            if (process == null)
            {
                throw new Exception("Process could not start!");
            }

            new ProcStdStreamObserver(process, process.StandardError, s => App.Log(s));
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
                    Arguments = $"-hide_banner -loglevel quiet -i \"{fPath}\" -ac 2 -f s16le -ar {sampleRate} pipe:1",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
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

            new ProcStdStreamObserver(process, process.StandardError, s => App.Log(s));
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
            App.Log("[WARNING] Stream backend explicitly disposed.");
            this._proc.Dispose();
        }
    }
}
