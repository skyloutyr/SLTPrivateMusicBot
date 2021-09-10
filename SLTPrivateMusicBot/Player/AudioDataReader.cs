namespace SLTPrivateMusicBot.Player
{
    using System;
    using System.IO;

    public class AudioDataReader : Stream
    {
        private readonly MemoryStream _underlyingMS;

        public AudioDataReader(AudioInfo ai)
        {
            ai.CreateBuffer();
            this._underlyingMS = new MemoryStream(ai.GetBuffer());
        }

        public override bool CanRead => this._underlyingMS.CanRead;

        public override bool CanSeek => this._underlyingMS.CanSeek;

        public override bool CanWrite => this._underlyingMS.CanWrite;

        public override long Length => this._underlyingMS.Length;

        public override long Position { get => this._underlyingMS.Position; set => this._underlyingMS.Position = value; }

        public override void Flush() => this._underlyingMS.Flush();
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count % 2 != 0)
            {
                throw new Exception("Can't read float data from a non-PCM buffer!");
            }

            byte[] tBuf = new byte[2];
            int c = 0;
            for (int i = 0; i < count / 2; ++i)
            {
                int r = this._underlyingMS.Read(tBuf);
                if (r <= 0)
                {
                    return 0;
                }

                short f = BitConverter.ToInt16(tBuf);
                f = (short)(f * AudioPlayer.Instance.Volume);
                tBuf = BitConverter.GetBytes(f);
                buffer[c++] = tBuf[0]; 
                buffer[c++] = tBuf[1]; 
            }

            return c;
        }

        public override long Seek(long offset, SeekOrigin origin) => this._underlyingMS.Seek(offset, origin);
        public override void SetLength(long value) => this._underlyingMS.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => this._underlyingMS.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                this._underlyingMS.Dispose();
            }
        }
    }
}
