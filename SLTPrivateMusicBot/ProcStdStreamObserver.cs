namespace SLTPrivateMusicBot
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class ProcStdStreamObserver
    {
        private Process _proc;
        private string _pName;
        private StreamReader _std;
        private StringBuilder _sb = new StringBuilder();
        private Action<string> _msgReporter;
        private bool _procDisposed;

        public ProcStdStreamObserver(Process proc, StreamReader std, Action<string> msgReporter)
        {
            this._proc = proc;
            this._pName = proc.ProcessName;
            this._std = std;
            this._msgReporter = msgReporter;
            this._proc.Disposed += (o, e) => this._procDisposed = true;
            this._proc.Exited += (o, e) => this._procDisposed = true;
            this._proc.EnableRaisingEvents = true;
            new Thread(this.Listen) { IsBackground = true }.Start();
        }

        public void Listen()
        {
            try
            {
                while (true)
                {
                    char c;
                    while (this._std.Peek() >= 0) // Have chars
                    {
                        this._sb.Append(c = (char)this._std.Read());
                        if (c == '\n')
                        {
                            this._msgReporter("[Proc](" + this._pName + ")" + this._sb.ToString());
                            this._sb.Clear();
                        }
                    }

                    if (this._proc.HasExited || this._procDisposed)
                    {
                        break;
                    }

                    Thread.Sleep(100);
                }
            }
            catch (Exception ode)
            {
                // Stream was disposed
                this._msgReporter("Stream for " + this._pName + " was disposed!");
            }
            finally
            {
                this._msgReporter("[Proc](" + this._pName + ")" + this._sb.ToString());
            }
        }
    }
}
