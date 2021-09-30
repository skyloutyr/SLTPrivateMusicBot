namespace SLTPrivateMusicBot.Player
{
    using System;
    using System.Threading;
    using System.Windows;

    public class AudioPlayer
    {
        private static EventWaitHandle _nextStopWait = new EventWaitHandle(false, EventResetMode.AutoReset);

        public static AudioPlayer Instance { get; } = new AudioPlayer();

        #region References
        public AudioInfo? CurrentAudio { get; set; }
        #endregion

        #region Tracking
        public int CurrentIndex { get; set; } = -1;
        public bool IsPlaying { get; set; }
        public bool Shuffle { get; set; }
        public bool WaitingForNext { get; set; }
        public bool StreamingEnabled { get; set; }
        public LoopMode LoopMode { get; set; }
        public float Volume { get; set; } = 1;
        #endregion

        public static void Add(AudioInfo info) => Application.Current.Dispatcher.Invoke(() => MainWindow.Playlist.Add(info));

        public void Remove(AudioInfo info)
        {
            int index = MainWindow.Playlist.IndexOf(info);
            if (index != -1)
            {
                if (index == this.CurrentIndex && this.CurrentAudio != null)
                {
                    this.IsPlaying = false;
                    MainWindow.Bot.StopAudio();
                    this.CurrentIndex = -1;
                    this.CurrentAudio = null;
                    Application.Current.Dispatcher.Invoke(() => MainWindow.Playlist.Remove(info));
                    return;
                }

                if (index > this.CurrentIndex)
                {
                    Application.Current.Dispatcher.Invoke(() => MainWindow.Playlist.Remove(info));
                    return;
                }

                if (index < this.CurrentIndex)
                {
                    this.CurrentIndex -= 1;
                    Application.Current.Dispatcher.Invoke(() => MainWindow.Playlist.Remove(info));
                    return;
                }
            }
        }

        public void Move(AudioInfo info, int to)
        {
            int oi = MainWindow.Playlist.IndexOf(info);
            if (oi < to)
            {
                to -= 1;
            }

            if (info == this.CurrentAudio)
            {
                this.CurrentIndex = to;
            }
            else
            {
                if (to < this.CurrentIndex)
                {
                    this.CurrentIndex += 1;
                }
            }

            Application.Current.Dispatcher.Invoke(() => MainWindow.Playlist.Move(oi, to));
        }

        public void Next()
        {
            if (this.IsPlaying)
            {
                int nIndex = this.SelectNextIndex();
                if (nIndex != -1)
                {
                    this.WaitingForNext = true;
                    Application.Current.Dispatcher.Invoke(() => MainWindow.Current.DisablePlay());
                    MainWindow.Bot.StopAudio(() => _nextStopWait.Set());
                    _nextStopWait.WaitOne();
                    Application.Current.Dispatcher.Invoke(() => MainWindow.Current.EnablePlay());
                    this.Play(nIndex);
                }
                else
                {
                    this.IsPlaying = false;
                    MainWindow.Bot.StopAudio();
                }
            }
        }

        public int SelectNextIndex()
        {
            if (this.LoopMode == LoopMode.Once) // Looping same audio file, just pick it again
            {
                if (this.CurrentAudio != null && MainWindow.Playlist.Contains(this.CurrentAudio))
                {
                    return this.CurrentIndex;
                }
                else
                {
                    return -1; // Can't loop audio that isn't in the playlist?
                }
            }

            int assumedNext = this.CurrentIndex + 1;
            if (assumedNext >= MainWindow.Playlist.Count) // We are at the end of the playlist
            {
                return this.LoopMode == LoopMode.Loop ? 0 : -1; // If we are looping return audio at index 0, otherwise can't play
            }

            return assumedNext; // Next audio
        }

        public void Play(int selectedIndex) // Play pressed
        {
            if (MainWindow.Playlist.Count > 0) // Have tracks to play
            {
                if (this.CurrentIndex != -1)
                {
                    if (selectedIndex != -1)
                    {

                    }
                    else
                    {

                    }
                }

                this.CurrentIndex = selectedIndex;
                this.IsPlaying = true;
                Application.Current.Dispatcher.Invoke(() => MainWindow.Current.PlayCallback());
                this.CurrentAudio = MainWindow.Playlist[this.CurrentIndex];
                MainWindow.Bot.Play(this.CurrentAudio);
            }
        }

        public void PlayFinishedCallback()
        {
            if (this.WaitingForNext) // Do nothing if the next command was triggered
            {
                this.WaitingForNext = false;
                return;
            }

            if (this.IsPlaying) // Do not do anything if not playling
            {
                this.CurrentIndex = this.SelectNextIndex();
                if (this.CurrentIndex != -1)
                {
                    this.CurrentAudio = MainWindow.Playlist[this.CurrentIndex];
                    MainWindow.Bot.Play(this.CurrentAudio);
                }
                else
                {
                    this.IsPlaying = false;
                    Application.Current.Dispatcher.Invoke(() => MainWindow.Current.StopCallback());
                }
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() => MainWindow.Current.StopCallback());
            }
        }

        public void Stop()
        {
            if (this.IsPlaying)
            {
                this.IsPlaying = false;
                MainWindow.Bot.StopAudio();
            }
        }
    }

    public enum LoopMode
    {
        None,
        Loop,
        Once
    }
}
