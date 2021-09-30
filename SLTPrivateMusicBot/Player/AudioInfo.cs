namespace SLTPrivateMusicBot.Player
{
    using System;
    using System.IO;

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
                string s = string.IsNullOrEmpty(this.Metadata.format.tags.title) ? Path.GetFileNameWithoutExtension(this.FilePath) : this.Metadata.format.tags.title;
                return s.Length < 64 ? s : string.Concat(s.AsSpan(0, 61), "...");
            }
        }

        public string FullNameProperty => string.IsNullOrEmpty(this.Metadata.format.tags.title) ? Path.GetFileNameWithoutExtension(this.FilePath) : this.Metadata.format.tags.title;

        public Func<byte[]> AudioGetter { get; set; }
        private byte[] _audioBuffer;

        public AudioInfo(string fPath, bool streaming = false)
        {
            if (File.Exists(fPath)) // Have a file
            {
                this.FilePath = fPath;
                this.Metadata = FFMpeg.GetAudioMetadata(fPath);
                this.Length = (uint)Math.Ceiling(double.Parse(this.Metadata.format.duration) * 1000);
                this.IsStreaming = streaming;
                if (streaming)
                {
                    this.StreamBackend = FFMpeg.StreamAudio(fPath, 48000);
                }
                else
                {
                    this.AudioGetter = () => FFMpeg.GetAudio(fPath, 48000);
                }
            }
            else
            {
                throw new ArgumentException("A file at a given path does not exist!");
            }
        }

        public void CreateBuffer()
        {
            if (this.IsStreaming)
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
            else
            {
                this._audioBuffer = this.AudioGetter();
            }
        }

        private readonly byte[] _internalBuffer = new byte[192000]; //~192KiB buffer

        public void ClearBuffer() => Array.Fill<byte>(this._internalBuffer, 0); // Flush buffer w 0s to prevent outputting garbage at EoF.

        public byte[] GetBuffer()
        {
            if (this.IsStreaming)
            {
                int w = 0;
                while (true) // Hate doing it this way, esp. broken w/ broken input files but oh well
                {            // TODO find a way to not have a while/true loop here, maybe embed ffmpeg and do stuff there?
                    int i = this.StreamBackend.BaseOut.Read(this._internalBuffer, w, 192000 - w);
                    if (i == 0 || w == 192000)
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
            this._audioBuffer = null;
            if (this.IsStreaming)
            {
                this.StreamBackend.Dispose();
            }
        }
    }
}
