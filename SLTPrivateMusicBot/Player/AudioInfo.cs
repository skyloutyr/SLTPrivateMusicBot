namespace SLTPrivateMusicBot.Player
{
    using System;
    using System.IO;

    public class AudioInfo
    {
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

        public AudioInfo(string fPath)
        {
            if (File.Exists(fPath)) // Have a file
            {
                this.FilePath = fPath;
                this.Metadata = FFMpeg.GetAudioMetadata(fPath);
                this.Length = (uint)Math.Ceiling(double.Parse(this.Metadata.format.duration) * 1000);
                this.AudioGetter = () => FFMpeg.GetAudio(fPath, 48000);
            }
            else
            {
                throw new ArgumentException("A file at a given path does not exist!");
            }
        }

        public void CreateBuffer() => this._audioBuffer = this.AudioGetter();

        public byte[] GetBuffer() => this._audioBuffer;
        public void Clear() => this._audioBuffer = null;
    }
}
