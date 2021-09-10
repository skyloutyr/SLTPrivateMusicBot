namespace SLTPrivateMusicBot
{
    using MahApps.Metro.Controls;
    using SLTPrivateMusicBot.Player;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Data;
    using System.Windows.Input;
    using System.Windows.Media;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        public static MainWindow Current => (MainWindow)Application.Current.MainWindow;

        public static BotCore? Bot { get; set; }
        public static ObservableCollection<AudioInfo> Playlist { get; } = new ObservableCollection<AudioInfo>();

        public MainWindow()
        {
            InitializeComponent();
            this.Closed += this.HandleClose;
        }

        private void HandleClose(object? sender, System.EventArgs e)
        {
            try
            {
                Bot?.Disconnect();
            }
            finally
            {
            }
        }

        public void ClientInVoiceCallback()
        {
            this.B_Indicator.Background = new SolidColorBrush(Colors.Green);
            this.B_Indicator.ToolTip = "Everything is setup correctly and ready to work.";
        }

        public void ClientDisconnectedCallback()
        {
            this.B_Indicator.Background = new SolidColorBrush(Colors.Red);
            this.B_Indicator.ToolTip = "No Connection.";
            this.Btn_Connect.IsEnabled = true;
        }

        public void ClientConnectedCallback()
        {
            this.B_Indicator.Background = new SolidColorBrush(Colors.Yellow);
            this.B_Indicator.ToolTip = "Connected, but not in a voice channel. Join by @ at the bot.";
            this.Btn_Connect.IsEnabled = true;
        }

        public void PlayCallback()
        {
            this.Btn_Play.Background = new SolidColorBrush(Colors.LightCyan);
        }

        public void StopCallback()
        {
            this.Btn_Play.Background = new SolidColorBrush(Colors.White);
            this.ProgressBar_Progress.Value = 0;
            this.Label_Current.Content = this.Label_Duration.Content = "00:00";
        }

        public void UpdatePlayingPath()
        {
            foreach (object? item in this.ListView_Playlist.Items)
            {
                ListViewItem lvi = (ListViewItem)this.ListView_Playlist.ItemContainerGenerator.ContainerFromItem(item);
                System.Windows.Shapes.Path p = lvi.FindChild<System.Windows.Shapes.Path>();
                p.Visibility = ((AudioInfo)item).IsPlaying ? Visibility.Visible : Visibility.Hidden;
            }
        }

        // Connect clicked
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.UpdatePlayingPath();
            if (Bot == null)
            {
                Bot = new BotCore();
                Bot.Awake();
                this.Btn_Connect.IsEnabled = false;
            }
        }

        private void ListView_Playlist_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent("SLTPMB:AudioInfo"))
            {
                ListViewItem? lvi = GetObjectUnderPoint<ListViewItem>(this.ListView_Playlist, e.GetPosition(this.ListView_Playlist));
                if (lvi != null && this.ListView_Playlist.SelectedItems.Contains(lvi.DataContext))
                {
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                    this.Line_DropPreview.Visibility = Visibility.Hidden;
                    return;
                }

                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }

            if (e.Data != null && (e.Data.GetDataPresent("FileDrop") || e.Data.GetDataPresent("UnicodeText")))
            {
                e.Effects = DragDropEffects.Copy;
                e.Handled = true;
            }
        }

        private void ListView_Playlist_DragLeave(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent("SLTPMB:AudioInfo"))
            {
                ListViewItem? lvi = GetObjectUnderPoint<ListViewItem>(this.ListView_Playlist, e.GetPosition(this.ListView_Playlist));
                if (lvi != null && this.ListView_Playlist.SelectedItems.Contains(lvi.DataContext))
                {
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                    this.Line_DropPreview.Visibility = Visibility.Hidden;
                    return;
                }

                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }
        private void ListView_Playlist_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data != null && e.Data.GetDataPresent("SLTPMB:AudioInfo"))
            {
                ListViewItem? lvi = GetObjectUnderPoint<ListViewItem>(this.ListView_Playlist, e.GetPosition(this.ListView_Playlist));
                if (lvi != null && this.ListView_Playlist.SelectedItems.Contains(lvi.DataContext))
                {
                    e.Effects = DragDropEffects.None;
                    e.Handled = true;
                    this.Line_DropPreview.Visibility = Visibility.Hidden;
                    return;
                }
                else
                {
                    if (lvi != null)
                    {
                        this.Line_DropPreview.Visibility = Visibility.Visible;
                        Point p = e.GetPosition(lvi);
                        double halfPoint = lvi.ActualHeight / 2;
                        this.Line_DropPreview.X2 = this.ListView_Playlist.ActualWidth - 12;
                        if (p.Y >= halfPoint)
                        {
                            this.Line_DropPreview.Margin = new Thickness(0, e.GetPosition(this).Y + (lvi.ActualHeight - p.Y) - lvi.ActualHeight - 2, 0, 0);
                        }
                        else
                        {
                            this.Line_DropPreview.Margin = new Thickness(0, e.GetPosition(this).Y - p.Y - lvi.ActualHeight - 2, 0, 0);
                        }

                        this.Line_DropPreview.InvalidateVisual();
                    }
                }

                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private void ListView_Playlist_Drop(object sender, DragEventArgs e)
        {
            if (e.Data != null)
            {
                if (e.Data.GetDataPresent("SLTPMB:AudioInfo"))
                {
                    this.Line_DropPreview.Visibility = Visibility.Hidden;
                    ListViewItem? lvi = GetObjectUnderPoint<ListViewItem>(this.ListView_Playlist, e.GetPosition(this.ListView_Playlist));
                    if (lvi != null && !this.ListView_Playlist.SelectedItems.Contains(lvi.DataContext)) // Mouse over a viewitem, but that item isn't a selected one.
                    {
                        Point p = e.GetPosition(lvi);
                        double halfPoint = lvi.ActualHeight / 2;
                        bool insertAfter = p.Y >= halfPoint;
                        this.ListView_Playlist.SelectedItems.Clear();
                        AudioInfo[] ai = (AudioInfo[])e.Data.GetData("SLTPMB:AudioInfo");
                        Array.Sort(ai, (l, r) => Playlist.IndexOf(l).CompareTo(Playlist.IndexOf(r)));
                        for (int i = ai.Length - 1; i >= 0; i--)
                        {
                            AudioInfo info = ai[i];
                            int indexMoveTo = Playlist.IndexOf((AudioInfo)lvi.DataContext) + (insertAfter ? 1 : 0);
                            AudioPlayer.Instance.Move(info, indexMoveTo);
                        }
                    }

                    return;
                }

                if (e.Data.GetDataPresent("FileDrop"))
                {
                    string[] data = (string[])e.Data.GetData("FileDrop");
                    if (data.Length > 0)
                    {
                        this.ListView_Playlist.Visibility = Visibility.Hidden;
                        this.ProgressRing_Wait.Visibility = Visibility.Visible;
                    }

                    new Thread(() =>
                    {
                        foreach (string s in data)
                        {
                            if (new[] { ".mp3", ".wav", ".ogg", ".mp4" }.Contains(Path.GetExtension(s)))
                            {
                                AudioPlayer.Add(new AudioInfo(s));
                            }
                        }

                        Application.Current.Dispatcher.Invoke(() => {
                            Current.ListView_Playlist.Visibility = Visibility.Visible;
                            Current.ProgressRing_Wait.Visibility = Visibility.Hidden;
                        });

                    }).Start();
                }
                else
                {
                    if (e.Data.GetDataPresent("UnicodeText"))
                    {
                        Uri dataURI = new Uri((string)e.Data.GetData("UnicodeText"));
                        this.ListView_Playlist.Visibility = Visibility.Hidden;
                        this.ProgressRing_Wait.Visibility = Visibility.Visible;
                        new Thread(() =>
                        {
                            bool err = false;
                            try
                            {
                                string fPath = YoutubeDL.DownloadVideo(dataURI);
                                AudioPlayer.Add(new AudioInfo(fPath));
                            }
                            catch
                            {
                                err = true;
                            }

                            Application.Current.Dispatcher.Invoke(() => {
                                Current.ListView_Playlist.Visibility = Visibility.Visible;
                                Current.ProgressRing_Wait.Visibility = Visibility.Hidden;
                                if (err)
                                {
                                    MessageBox.Show("Either the provided link wasn't a youtube video url or there was a filesystem error", "Couldn't download video!", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            });
                        }).Start();
                    }
                }
            }
        }

        private void ListView_Playlist_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && e.LeftButton.HasFlag(MouseButtonState.Pressed) && this.ListView_Playlist.SelectedItems != null && this.ListView_Playlist.SelectedItems.Count > 0)
            {
                AudioInfo[] draggedItems = this.ListView_Playlist.SelectedItems.Cast<AudioInfo>().ToArray();
                DataObject da = new DataObject("SLTPMB:AudioInfo", draggedItems);
                DragDrop.DoDragDrop(this.ListView_Playlist, da, DragDropEffects.Move);
            }
        }

        private ListViewItem? _contextItem;
        private void ListView_Playlist_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this._contextItem = null;
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && this.ListView_Playlist.SelectedItems != null && this.ListView_Playlist.SelectedItems.Count > 0) // Have things selected!
            {
                ListViewItem? item = GetObjectUnderPoint<ListViewItem>(this.ListView_Playlist, e.GetPosition(this.ListView_Playlist));
                if (item != null && item.DataContext is AudioInfo && this.ListView_Playlist.SelectedItems.Contains(item.DataContext)) // Left clicking a selected item
                {
                    this._contextItem = item;
                    e.Handled = true;
                }
            }
        }

        private void ListView_Playlist_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            this.Line_DropPreview.Visibility = Visibility.Hidden;
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) && !Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && this.ListView_Playlist.SelectedItems != null && this.ListView_Playlist.SelectedItems.Count > 0 && this._contextItem != null) // Have things selected!
            {
                ListViewItem? item = GetObjectUnderPoint<ListViewItem>(this.ListView_Playlist, e.GetPosition(this.ListView_Playlist));
                if (item != null && item == this._contextItem) // Mouse up over the same element that was clicked initially, reset selection
                {
                    this.ListView_Playlist.SelectedItems.Clear();
                    this.ListView_Playlist.SelectedItems.Add(item.DataContext);
                    this.ListView_Playlist.SelectedItem = item.DataContext;
                    this.ListView_Playlist.SelectedIndex = Playlist.IndexOf((AudioInfo)item.DataContext);
                    this.ListView_Playlist.UpdateLayout();
                    this._contextItem = null;
                }
            }
        }

        public static T? GetObjectUnderPoint<T>(Visual reference, Point p) where T : DependencyObject
        {
            DependencyObject? item = VisualTreeHelper.HitTest(reference, p)?.VisualHit;
            while (item is not null and not T)
            {
                item = VisualTreeHelper.GetParent(item);
            }

            return item is T t ? t : null;
        }

        private void Btn_Play_Click(object sender, RoutedEventArgs e)
        {
            AudioPlayer.Instance.Play(0);
        }

        private void Btn_Repeat_Click(object sender, RoutedEventArgs e)
        {
            switch (AudioPlayer.Instance.LoopMode)
            {
                case LoopMode.None: // Goto loop
                {
                    AudioPlayer.Instance.LoopMode = LoopMode.Loop;
                    this.Btn_Repeat.Background = new SolidColorBrush(Colors.LightCyan);
                    break;
                }

                case LoopMode.Loop: // Goto once
                {
                    AudioPlayer.Instance.LoopMode = LoopMode.Once;
                    this.Path_Repeat.Visibility = Visibility.Hidden;
                    this.Path_RepeatOnce.Visibility = Visibility.Visible;
                    break;
                }

                case LoopMode.Once: // Goto none
                {
                    AudioPlayer.Instance.LoopMode = LoopMode.None;
                    this.Path_Repeat.Visibility = Visibility.Visible;
                    this.Path_RepeatOnce.Visibility = Visibility.Hidden;
                    this.Btn_Repeat.Background = new SolidColorBrush(Colors.White);
                    break;
                }
            }
        }

        private void Btn_Stop_Click(object sender, RoutedEventArgs e)
        {
            AudioPlayer.Instance.Stop();
        }

        // Delete
        private void CommandBinding_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            if (this.ListView_Playlist.SelectedItems != null && this.ListView_Playlist.SelectedItems.Count > 0)
            {
                e.CanExecute = true;
                e.Handled = true;
            }
            else
            {
                e.CanExecute = false;
            }
           
        }

        // Delete
        private void CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            List<AudioInfo> toDelete = new List<AudioInfo>();
            foreach (object? item in this.ListView_Playlist.SelectedItems)
            {
                if (item is AudioInfo ai)
                {
                    toDelete.Add(ai);
                }
            }

            foreach (AudioInfo ai in toDelete)
            {
                AudioPlayer.Instance.Remove(ai);
            }
        }

        private void Btn_Next_Click(object sender, RoutedEventArgs e)
        {
            AudioPlayer.Instance.Next();
        }

        private void Slider_Volume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            AudioPlayer.Instance.Volume = (float)Math.Clamp(this.Slider_Volume.Value / 100, 0, 1);
        }
    }
}
