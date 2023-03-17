namespace SLTPrivateMusicBot.Player
{
    using System;
    using System.IO;

    public class AudioDataReader : Stream
    {
        private AudioInfo _ai;
        private readonly MemoryStream _underlyingMS;

        public AudioDataReader(AudioInfo ai)
        {
            this._ai = ai;
            if (!ai.IsStreaming)
            {
                ai.CreateBuffer();
                this._underlyingMS = new MemoryStream(ai.GetBuffer());
            }
            else
            {
                this._underlyingMS = new MemoryStream(new byte[0]);
                this._streamingApproxLength = (long)(this._ai.Length / 1000f * 192000);
            }
        }

        public override bool CanRead => this._underlyingMS.CanRead;

        public override bool CanSeek => this._underlyingMS.CanSeek;

        public override bool CanWrite => this._underlyingMS.CanWrite;

        public override long Length => this._ai.IsStreaming ? this._streamingApproxLength : this._underlyingMS.Length;

        public override long Position
        {
            get => this._ai.IsStreaming ? this._streamingPosition : this._underlyingMS.Position;
            set
            {
                if (this._ai.IsStreaming)
                {
                    float f = (float)value / (float)this._streamingApproxLength;
                    if (f > 1 - float.Epsilon)
                    {
                        f = 1;
                    }

                    this._ai.Seek(f);
                    this._streamingPosition = value;
                }
                else
                {
                    this._underlyingMS.Position = value;
                }
            }
        }

        public override void Flush() => this._underlyingMS.Flush();

        private long _streamingApproxLength;
        private long _streamingPosition;
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count % 2 != 0)
            {
                throw new Exception("Can't read float data from a non-PCM buffer!");
            }

            byte[] b = null;
            if (this._ai.IsStreaming)
            {
                if (this._streamingApproxLength - this._streamingPosition <= MainWindow.MusicRate)
                {
                    this._ai.ClearBuffer();
                }

                b = this._ai.GetBuffer();
                if (b.Length == 0)
                {
                    return 0;
                }

                this._streamingPosition = Math.Min(this._streamingApproxLength, this._streamingPosition + MainWindow.MusicRate);
            }

            byte[] tBuf = new byte[2];
            int c = 0;
            for (int i = 0; i < count / 2; ++i)
            {
                if (this._ai.IsStreaming)
                {
                    tBuf[0] = b[c];
                    tBuf[1] = b[c + 1];
                }
                else
                {
                    int r = this._underlyingMS.Read(tBuf);
                    if (r <= 0)
                    {
                        return 0;
                    }
                }

                short f = BitConverter.ToInt16(tBuf); // Signed 16bit PCM
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
