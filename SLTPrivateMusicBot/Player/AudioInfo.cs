namespace SLTPrivateMusicBot.Player
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Threading;

    public class AudioInfo
    {
        public bool IsStreaming { get; set; }
        public StreamBackend StreamBackend { get; set; }


        public string FilePath { get; set; }
        public AudioMetadata Metadata { get; set; }
        public uint Length { get; set; } // In MS

        public bool IsBeingCleanedUp { get; set; }
        public bool IsPlaying { get; set; }

        public string LengthProperty => $"{this.Length / 1000 / 60:00}:{this.Length / 1000 % 60:00}";
        public string NameProperty
        {
            get
            {
                string s = this.FullNameProperty;
                return s.Length < 64 ? s : string.Concat(s.AsSpan(0, 61), "...");
            }
        }

        public string FullNameProperty => !string.IsNullOrEmpty(this._overrideTitle) ? this._overrideTitle : string.IsNullOrEmpty(this.Metadata.format.tags.title) ? Path.GetFileNameWithoutExtension(this.FilePath) : this.Metadata.format.tags.title;

        public Func<byte[]> AudioGetter { get; set; }
        public bool IsYTDirect => this._isYoutubeStream;

        private byte[] _audioBuffer;
        private readonly string _originalURL;
        private readonly bool _isYoutubeStream;
        private long _expireDate;
        private string _overrideTitle;

        public AudioInfo(string fPath, bool streaming = false, bool ytDirect = false)
        {
            if (ytDirect && fPath.StartsWith("https://"))
            {
                if (!streaming)
                {
                    App.Log("[FATAL] To use YTDirect Audio streaming must also be enabled!");
                    throw new ArgumentException("To use YTDirect Audio streaming must also be enabled!");
                }

                this._originalURL = fPath;
                this._isYoutubeStream = true;
                this.IsStreaming = true;
                this.FilePath = Uri.UnescapeDataString(YoutubeDL.GetVideoStreamUrl(new Uri(fPath))).Replace("\n", "");
                if (string.IsNullOrEmpty(this.FilePath))
                {
                    App.Log("[FATAL] Couldn't get audio data from a given URL!");
                    throw new ArgumentException("Audio data could not be obtained from the URL");
                }

                App.Log("[FINE] Parsing audio file metadata");
                this.Metadata = FFMpeg.GetAudioMetadata(this.FilePath);
                this._overrideTitle = YoutubeDL.GetVideoTitle(new Uri(fPath)).Replace("\n", "");
                App.Log("[FINE] Have audio metadata");
                this._expireDate = YoutubeDL.GetExpirationDate(this.FilePath);
                double d = 0;
                try 
                { 
                    try
                    {
                        d = double.Parse(this.Metadata.format.duration, CultureInfo.CurrentCulture);
                    }
                    catch (FormatException fe)
                    {
                        d = double.Parse(this.Metadata.format.duration, CultureInfo.InvariantCulture);
                    }

                    this.Length = (uint)Math.Ceiling(d * 1000);
                } 
                catch (Exception e)
                {
                    App.Log("[ERROR] Audio metadata malformed!", e);
                }

                this.StreamBackend = FFMpeg.StreamAudio(this.FilePath, 48000);
                App.Log("[FINE] AudioInfo created!");
            }
            else
            {
                if (File.Exists(fPath)) // Have a file
                {
                    this.FilePath = fPath;
                    App.Log("[FINE] Parsing audio file metadata");
                    this.Metadata = FFMpeg.GetAudioMetadata(fPath);
                    App.Log("[FINE] Have audio metadata");
                    try
                    {
                        double d = 0;
                        try
                        {
                            d = double.Parse(this.Metadata.format.duration, CultureInfo.CurrentCulture);
                        }
                        catch (FormatException fe)
                        {
                            d = double.Parse(this.Metadata.format.duration, CultureInfo.InvariantCulture);
                        }

                        this.Length = (uint)Math.Ceiling(d * 1000);
                    }
                    catch (Exception e)
                    {
                        App.Log("[ERROR] Audio metadata malformed!", e);
                    }

                    this.IsStreaming = streaming;
                    if (streaming)
                    {
                        this.StreamBackend = FFMpeg.StreamAudio(fPath, 48000);
                    }
                    else
                    {
                        this.AudioGetter = () => FFMpeg.GetAudio(fPath, 48000);
                    }

                    App.Log("[FINE] AudioInfo created!");
                }
                else
                {
                    App.Log("[FATAL] Asked to add a non-existing file!");
                    throw new ArgumentException("A file at a given path does not exist!");
                }
            }
        }

        public void CreateBuffer()
        {
            if (this.IsStreaming)
            {
                if (this._isYoutubeStream)
                {
                    // Check expiration
                    long delta = this._expireDate - DateTimeOffset.Now.ToUnixTimeSeconds();
                    bool expires = delta <= 4; // expire in 4 seconds, recreate
                    bool canRead = this.StreamBackend.CanRead();
                    if (expires || !canRead)
                    {
                        App.Log("[INFO] YT stream expired, recreating");
                        this.FilePath = Uri.UnescapeDataString(YoutubeDL.GetVideoStreamUrl(new Uri(this._originalURL))).Replace("\n", "");
                        this._expireDate = YoutubeDL.GetExpirationDate(this.FilePath);
                        try
                        {
                            this.StreamBackend.Dispose();
                        }
                        catch
                        {
                            // NOOP
                        }

                        this.StreamBackend = FFMpeg.StreamAudio(this.FilePath, 48000);
                    }
                }
                else
                {
                    if (!this.StreamBackend.CanRead())
                    {
                        try
                        {
                            this.StreamBackend.Dispose(); // Dispose just in case?
                        }
                        finally
                        {
                            this.StreamBackend = FFMpeg.StreamAudio(this.FilePath, 48000); // Make self reuseable
                        }
                    }
                }
            }
            else
            {
                this._audioBuffer = this.AudioGetter();
            }
        }

        public void Seek(float percentage)
        {
            if (this.Length > 0)
            {
                int desired = (int)MathF.Round((this.Length / 1000) * percentage);
                App.Log("Seeking to " + desired);
                this.StreamBackend.Dispose();
                this.StreamBackend = FFMpeg.StreamAudio(this.FilePath, 48000, desired);
                this.ClearBuffer();
            }
        }

        private readonly byte[] _internalBuffer = new byte[192000]; //~192KiB buffer

        public void ClearBuffer() => Array.Fill<byte>(this._internalBuffer, 0); // Flush buffer w 0s to prevent outputting garbage at EoF.

        public byte[] GetBuffer()
        {
            if (this.IsStreaming)
            {
                int w = 0;
                int nTriesAt0 = 0;
                while (true) // Hate doing it this way, esp. broken w/ broken input files but oh well
                {            // TODO find a way to not have a while/true loop here, maybe embed ffmpeg and do stuff there?
                    int i = 0;
                    try
                    {
                        i = this.StreamBackend.BaseOut.Read(this._internalBuffer, w, MainWindow.MusicRate - w);
                        if (i == 0 && w == 0 && this.StreamBackend.CanRead() && nTriesAt0 < 64)
                        {
                            Debugger.Log(0, "", "Trying to read again, got 0 but not EOS (try " + nTriesAt0 + ")\n");
                            ++nTriesAt0;
                            continue;
                        }
                    }
                    catch (Exception e)
                    {
                        // FFMpeg died for no reason...
                        App.Log("[ERROR] FFMpeg died", e);
                    }

                    if (i == 0 || w == MainWindow.MusicRate)
                    {
                        break;
                    }

                    w += i;
                }

                if (w == 0)
                {
                    return Array.Empty<byte>();
                }

                return this._internalBuffer;
            }
            else
            {
                return this._audioBuffer;
            }
        }

        public void Clear()
        {
            App.Log("Clearing audio info for " + this.NameProperty);
            this._audioBuffer = null;
            if (this.IsStreaming)
            {
                this.StreamBackend.Dispose();
            }
        }
    }
}
