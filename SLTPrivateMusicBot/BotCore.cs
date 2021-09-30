namespace SLTPrivateMusicBot
{
    using Discord;
    using Discord.Audio;
    using Discord.Commands;
    using Discord.WebSocket;
    using SLTPrivateMusicBot.Player;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;

    public class BotCore
    {
        private DiscordSocketClient _client;

        private IAudioClient _currentAudioClient;


        private ulong _vcID;
        private ulong _gID;

        public IUserMessage PlaylistMessage { get; set; }

        private AudioInfo _currentAI;

        private readonly EventWaitHandle _waitOneToStop = new (false, EventResetMode.AutoReset);
        private readonly EventWaitHandle _waitOneToReconnect = new (false, EventResetMode.AutoReset);

        public void Awake()
        {
            new Thread(this.Run) { IsBackground = true }.Start();
        }

        public async void Run()
        {
            this._client = new DiscordSocketClient();
            this._client.Log += Log;
            this._client.MessageReceived += HandleCommand;
            this._client.Connected += this.HandleConnected;
            this._client.Disconnected += this.HandleDisconnected;
            this._client.UserVoiceStateUpdated += this.HandleVoiceUpdated;
            await this._client.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("SLTPrivateMusicBotToken"));
            await this._client.StartAsync();
            await Task.Delay(-1);
        }

        private async Task HandleVoiceUpdated(SocketUser arg1, SocketVoiceState arg2, SocketVoiceState arg3)
        {
            if (arg1.Id == this._client.CurrentUser.Id) // We are interacting with the voice channels
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MainWindow mw = (MainWindow)Application.Current.MainWindow;
                    mw.ClientInVoiceCallback();
                });
            }

            await Task.CompletedTask;
        }

        private async Task HandleDisconnected(Exception arg)
        {
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MainWindow mw = (MainWindow)Application.Current.MainWindow;
                    if (mw != null)
                    {
                        mw.ClientDisconnectedCallback();
                    }
                });
            }

            await Task.CompletedTask;
        }

        private async Task HandleConnected()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MainWindow mw = (MainWindow)Application.Current.MainWindow;
                mw.ClientConnectedCallback();
            });

            await Task.CompletedTask;
        }

        private async Task HandleCommand(SocketMessage arg)
        {
            if (arg is SocketUserMessage sum)
            {
                if (sum.MentionedUsers.Any(u => u.Mention.Equals(this._client.CurrentUser.Mention)) && !sum.Author.IsBot && sum.Author is SocketGuildUser sgu && sgu.GuildPermissions.Administrator)
                {
                    if (sum.Author is IGuildUser igu)
                    {
                        IVoiceChannel ivc = igu.VoiceChannel;
                        if (ivc != null)
                        {
#pragma warning disable CS4014 // Must be executed outside of current thread due to gateway timeout.
                            this.JoinVoice(ivc);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        }
                    }
                }
            }

            await Task.CompletedTask;
        }

        public static async Task Log(LogMessage msg)
        {
            System.Diagnostics.Debug.WriteLine(msg);
            await Task.CompletedTask;
        }

        public static async Task Log(string msg)
        {
            System.Diagnostics.Debug.WriteLine(msg);
            await Task.CompletedTask;
        }

        public async Task LeaveVoice()
        {
            if (this._currentAudioClient != null)
            {
                this.StopAudio();
                await this._currentAudioClient.StopAsync();
                this._currentAudioClient.Dispose();
                this._currentAudioClient = null;
            }
        }

        public async Task JoinVoice(IVoiceChannel channel)
        {
            try
            {
                if (this._currentAudioClient != null)
                {
                    await this._currentAudioClient.StopAsync();
                }

                this._currentAudioClient = await channel.ConnectAsync(true, false);
                this._vcID = channel.Id;
                this._gID = channel.GuildId;
                await Log("Audio client connected");
            }
            catch (Exception)
            {
                System.Diagnostics.Debugger.Break();
            }
        }

        public async void RejoinVoice() // Attemp to rejoin the voice channel we were last in
        {
            if (this._vcID != 0)
            {
                try
                {
                    this._currentAudioClient.Dispose(); // Free unmanaged just in case.
                }
                finally
                {
                    this._currentAudioClient = null;
                    this._currentAudioClient = await this._client.GetGuild(this._gID).GetVoiceChannel(this._vcID).ConnectAsync();
#pragma warning disable CS4014 // The point is to run this async
                    Task.Run(async () =>
                    {
                        long l = 0;
                        while (this._currentAudioClient == null && l < 10000) // wait 10 seconds
                        {
                            await Task.Delay(100);
                            l += 100;
                        }

                        this._waitOneToReconnect.Set();
                    });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                }
            }
        }

        private Action _stopCallback;
        public void StopAudio(Action callback = null)
        {
            if (this._currentAI != null)
            {
                this._currentAI.IsBeingCleanedUp = true;
                this._stopCallback = callback;
            }
            else
            {
                callback?.Invoke();
            }
        }

        public static string Shorten(string text, int maxLen) => text.Length <= maxLen ? text : string.Concat(text.AsSpan(0, maxLen - 3), "...");

        private readonly byte[] _buffer = new byte[48000 * 2 * 2]; // Raw data is s16le * 48000 sample rate * 2 channels
        public void Play(AudioInfo info)
        {
            if (this._currentAudioClient == null)
            {
                return;
            }

            this.StopAudio();
            new Thread(() =>
            {
                if (this._currentAI != null)
                {
                    this._waitOneToStop.WaitOne();
                }


                this._currentAI = info;
                AudioDataReader? output = null;
                AudioOutStream? d = null;
                try
                {
                    info.CreateBuffer();
                    output = new AudioDataReader(info);
                    Application.Current.Dispatcher.Invoke(() => 
                    { 
                        info.IsPlaying = true;
                        MainWindow.Current.Title = info.FullNameProperty;
                        MainWindow.Current.UpdatePlayingPath();
                    });

                    d = this._currentAudioClient.CreatePCMStream(AudioApplication.Music);
                    try
                    {
                        this._client.SetGameAsync(Shorten(info.FullNameProperty, 25));
                        int l = 0;
                        while ((l = output.Read(this._buffer)) > 0 && !info.IsBeingCleanedUp)
                        {
                            long c = output.Position;
                            long t = output.Length;
                            double p = (double)c / (double)t;
                            try
                            {
                                d.Write(this._buffer, 0, l);
                            }
                            catch (Exception ex)
                            {
                                // stream must be closed.
                                if (this._currentAudioClient == null || this._currentAudioClient.ConnectionState != ConnectionState.Connected)
                                {
                                    try
                                    {
                                        d.Dispose(); // Try disposing just in case to free unmanaged
                                    }
                                    finally
                                    {
                                        this.RejoinVoice();
                                        this._waitOneToReconnect.WaitOne();
                                        if (this._currentAudioClient == null)
                                        {
                                            // Welp, couldn't even reconnect to voice. Just throw.
#pragma warning disable CA2219 // Not supposed to do this, but this is a fatal exception, can't continue execution anyway
                                            throw ex;
#pragma warning restore CA2219 // Do not raise exceptions in finally clauses
                                        }
                                    }
                                }
                                
                                // Try to re-create the stream and keep on keeping on?
                                d = this._currentAudioClient.CreatePCMStream(AudioApplication.Music);
                                d.Write(this._buffer, 0, l); // Just send the data again.
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    MainWindow mw = (MainWindow)Application.Current.MainWindow;
                                    mw.ClientInVoiceCallback();
                                });
                            }

                            Task.Run(() =>
                                Application.Current.Dispatcher.Invoke(() => {
                                    MainWindow.Current.ProgressBar_Progress.Value = p * 100;
                                    AudioInfo ai = Player.AudioPlayer.Instance.CurrentAudio;
                                    if (ai != null)
                                    {
                                        uint numSecs = (uint)(ai.Length * p);
                                        MainWindow.Current.Label_Current.Content = $"{numSecs / 1000 / 60:00}:{numSecs / 1000 % 60:00}";
                                        MainWindow.Current.Label_Duration.Content = ai.LengthProperty;
                                    }
                                })
                            );
                        }
                    }
                    finally
                    {
                        if (!info.IsBeingCleanedUp)
                        {
                            d.Flush();
                        }
                        else
                        {
                            this._stopCallback?.Invoke();
                        }
                        
                        info.Clear();
                        Application.Current.Dispatcher.Invoke(() => 
                        { 
                            info.IsPlaying = false;
                            MainWindow.Current.Title = "SLTPrivateMusicBot";
                            MainWindow.Current.UpdatePlayingPath();
                        });

                        info.IsBeingCleanedUp = false;
                        d.Dispose();
                        output.Dispose();
                    }
                }
                finally
                {
                    try
                    {
                        output?.Dispose();
                    }
                    catch
                    {
                        // NOOP
                    }

                    try
                    {
                        d?.Dispose();
                    }
                    catch
                    {
                        // NOOP
                    }

                    if (this._currentAI != null)
                    {
                        this._currentAI.Clear();
                        Application.Current.Dispatcher.Invoke(() => 
                        { 
                            this._currentAI.IsPlaying = false;
                            MainWindow.Current.Title = "SLTPrivateMusicBot";
                            MainWindow.Current.UpdatePlayingPath();
                        }); 

                        this._currentAI = null;
                        this._client.SetGameAsync(string.Empty);
                        AudioPlayer.Instance.PlayFinishedCallback();
                        this._waitOneToStop.Set();
                        // TODO signal player for next track
                    }
                }
            })
            { Priority = ThreadPriority.Highest, IsBackground = true }.Start();
        }

        public async void Disconnect()
        {
            await this._client.LogoutAsync();
            await this._client.StopAsync();
            this._client.Dispose();
        }
    }
}
