namespace SLTPrivateMusicBot.Player
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Windows;

    public class AudioPlayer
    {
        private static EventWaitHandle _nextStopWait = new EventWaitHandle(false, EventResetMode.AutoReset);
        private bool shuffle;

        public static AudioPlayer Instance { get; } = new AudioPlayer();

        #region References
        public AudioInfo? CurrentAudio { get; set; }
        #endregion

        #region Tracking
        public int CurrentIndex { get; set; } = -1;
        public bool IsPlaying { get; set; }
        public bool Shuffle
        {
            get => this.shuffle; 
            set
            {
                this.shuffle = value;
                if (value)
                {
                    this.Reshuffle();
                }
            }
        }

        public bool WaitingForNext { get; set; }
        public bool StreamingEnabled { get; set; }
        public bool YTDirectEnabled { get; set; }
        public LoopMode LoopMode { get; set; }
        public float Volume { get; set; } = 1;
        public bool Paused { get; internal set; }
        public Queue<int> ShuffleIndices { get; } = new Queue<int>();
        #endregion

        public void Add(AudioInfo info) => Application.Current.Dispatcher.Invoke(() =>
        {
            App.Log("[FINE] Adding audio sample to playlist.");
            MainWindow.Playlist.Add(info);
            if (this.Shuffle)
            {
                this.Reshuffle();
            }
        });

        public void Reshuffle()
        {
            if (MainWindow.Playlist.Count == 0)
            {
                this.ShuffleIndices.Clear();
            }
            else
            {
                List<int> qL = new List<int>(MainWindow.Playlist.Count);
                for (int i = 0; i < MainWindow.Playlist.Count; ++i)
                {
                    qL.Add(i);
                }

                ShuffleList(qL);
                this.ShuffleIndices.Clear();
                foreach (int i in qL)
                {
                    this.ShuffleIndices.Enqueue(i);
                }
            }
        }

        public static void ShuffleList<T>(IList<T> list)
        {
            int n = list.Count;
            Random rnd = new Random();
            while (n > 1)
            {
                int k = (rnd.Next(0, n) % n);
                n--;
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        public void Remove(AudioInfo info)
        {
            int index = MainWindow.Playlist.IndexOf(info);
            if (index != -1)
            {
                if (index == this.CurrentIndex && this.CurrentAudio != null)
                {
                    this.IsPlaying = false;
                    this.Paused = false;
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

            if (this.Shuffle)
            {
                this.Reshuffle();
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
            if (this.Shuffle)
            {
                this.Reshuffle();
            }
        }

        public void Switch(int index)
        {
            if (this.IsPlaying)
            {
                this.WaitingForNext = true;
                this.Paused = false;
                Application.Current.Dispatcher.Invoke(() => MainWindow.Current.DisablePlay());
                MainWindow.Bot.StopAudio(() => _nextStopWait.Set());
                _nextStopWait.WaitOne();
                Application.Current.Dispatcher.Invoke(() => MainWindow.Current.EnablePlay());
            }

            this.Play(index);
            if (this.Shuffle)
            {
                this.Reshuffle();
            }
        }

        public void Next()
        {
            if (this.IsPlaying)
            {
                int nIndex = this.CurrentIndex + 1;
                if (nIndex == MainWindow.Playlist.Count)
                {
                    nIndex = 0;
                }

                this.Switch(nIndex);
            }
        }

        public void Previous()
        {
            if (this.IsPlaying)
            {
                int nIndex = this.CurrentIndex - 1;
                if (nIndex == -1)
                {
                    nIndex = MainWindow.Playlist.Count - 1;
                }

                this.Switch(nIndex);
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

            int assumedNext;
            if (this.Shuffle)
            {
                if (this.ShuffleIndices.Count == 0)
                {
                    this.Reshuffle();
                }

                assumedNext = this.ShuffleIndices.Dequeue();
            }
            else
            {
                assumedNext = this.CurrentIndex + 1;
            }

            if (assumedNext >= MainWindow.Playlist.Count) // We are at the end of the playlist
            {
                return this.LoopMode == LoopMode.Loop ? 0 : -1; // If we are looping return audio at index 0, otherwise can't play
            }

            return assumedNext; // Next audio
        }

        public void Play(int selectedIndex) // Play pressed
        {
            if (MainWindow.Playlist.Count > 0 && MainWindow.Bot != null) // Have tracks to play
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
                this.Paused = false;
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
                this.Paused = false;
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
            this.IsPlaying = false;
            this.Paused = false;
            MainWindow.Bot.StopAudio();
        }
    }

    public enum LoopMode
    {
        None,
        Loop,
        Once
    }
}
