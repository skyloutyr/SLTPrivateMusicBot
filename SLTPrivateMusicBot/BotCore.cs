﻿namespace SLTPrivateMusicBot
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

        public bool Connected { get; set; }

        private AudioInfo _currentAI;

        private bool _waitingOneToStop;
        private readonly EventWaitHandle _waitOneToStop = new (false, EventResetMode.AutoReset);
        private readonly EventWaitHandle _waitOneToReconnect = new (false, EventResetMode.AutoReset);

        public BotCore()
        {
            this._buffer = new byte[MainWindow.MusicRate];
        }

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

                this.Connected = true;
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

                this.Connected = false;
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

            this.Connected = false;
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
            App.Log(msg.Message);
            System.Diagnostics.Debug.WriteLine(msg);
            await Task.CompletedTask;
        }

        public static async Task Log(string msg)
        {
            App.Log(msg);
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
                this.Connected = false;
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
            catch (Exception e)
            {
                App.Log("[ERROR] Could not connect to voice!", e);
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
                this._stopCallback = callback;
                this._currentAI.IsBeingCleanedUp = true;
            }
            else
            {
                callback?.Invoke();
            }
        }

        public static string Shorten(string text, int maxLen) => text.Length <= maxLen ? text : string.Concat(text.AsSpan(0, maxLen - 3), "...");

        public void SeekPosition(int percentage)
        {
            if (this._currentAI != null && !this._currentAI.IsBeingCleanedUp)
            {
                this._newSeekPosition = percentage;
            }
        }

        private int _newSeekPosition = -1;
        private readonly byte[] _buffer; // Raw data is s16le * 48000 sample rate * 2 channels

        public static StreamBackend ytTestStreamBackend;

        public void Play(AudioInfo info)
        {
            if (this._currentAudioClient == null || this._waitingOneToStop)
            {
                return;
            }

            this.StopAudio();
            new Thread(() =>
            {
                if (this._currentAI != null)
                {
                    this._waitingOneToStop = true;
                    this._waitOneToStop.WaitOne();
                    this._waitingOneToStop = false;
                }

                bool graceful = true;
                this._currentAI = info;
                this._newSeekPosition = -1;
                AudioDataReader? input = null;
                AudioOutStream? d = null;
                try
                {
                    info.CreateBuffer();
                    input = new AudioDataReader(info);
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
                        while (graceful && !info.IsBeingCleanedUp)
                        {
                            if (this._newSeekPosition >= 0)
                            {
                                d.Flush();
                                input.Position = (long)(input.Length * (this._newSeekPosition / 100f));
                                this._newSeekPosition = -1;
                            }

                            l = input.Read(this._buffer);
                            if (l <= 0)
                            {
                                break;
                            }

                            long c = input.Position;
                            long t = input.Length;
                            double p = (double)c / (double)t;
                            try
                            {
                                d.Write(this._buffer, 0, l);
                                while (AudioPlayer.Instance.Paused)
                                {
                                    Thread.Sleep(250);
                                }
                            }
                            catch (Exception ex)
                            {
                                App.Log("[ERROR] Issue while writing data to discord!", ex);
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
                                            graceful = false;
                                            // Welp, couldn't even reconnect to voice.
                                        }
                                    }
                                }

                                // Try to re-create the stream and keep on keeping on?
                                try
                                {
                                    d = this._currentAudioClient.CreatePCMStream(AudioApplication.Music);
                                    d.Write(this._buffer, 0, l); // Just send the data again.
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        MainWindow mw = (MainWindow)Application.Current.MainWindow;
                                        mw.ClientInVoiceCallback();
                                    });
                                }
                                catch (Exception exe)
                                {
                                    graceful = false;
                                    App.Log("[FATAL] Could not recover from discord issues", exe);
                                }
                            }

                            Task.Run(() =>
                                Application.Current.Dispatcher.Invoke(() => {
                                    if (MainWindow.Current != null)
                                    {
                                        MainWindow.Current.IgnoreSliderValueChange = true;
                                        MainWindow.Current.ProgressBar_Progress.Value = p * 100;
                                        MainWindow.Current.IgnoreSliderValueChange = false;
                                        AudioInfo ai = Player.AudioPlayer.Instance.CurrentAudio;
                                        if (ai != null)
                                        {
                                            uint numSecs = (uint)(ai.Length * p);
                                            MainWindow.Current.Label_Current.Content = $"{numSecs / 1000 / 60:00}:{numSecs / 1000 % 60:00}";
                                            MainWindow.Current.Label_Duration.Content = ai.LengthProperty;
                                        }
                                    }
                                })
                            );
                        }
                    }
                    catch (Exception e)
                    {
                        App.Log("[FATAL] Had a fatal issue streaming to Discord", e);
                    }
                    finally
                    {
                        if (!info.IsBeingCleanedUp)
                        {
                            d.Flush();
                        }
                        else
                        {
                            try
                            {
                                d.Write(new byte[MainWindow.MusicRate / 4]);
                                d.Flush();
                            }
                            catch
                            {
                                // NOOP
                            }
                        }

                        this._stopCallback?.Invoke();
                        this._stopCallback = null;
                        info.Clear();
                        Application.Current.Dispatcher.Invoke(() => 
                        { 
                            info.IsPlaying = false;
                            MainWindow.Current.Title = "SLTPrivateMusicBot";
                            MainWindow.Current.UpdatePlayingPath();
                        });

                        info.IsBeingCleanedUp = false;
                        d.Dispose();
                        input.Dispose();
                    }
                }
                finally
                {
                    try
                    {
                        input?.Dispose();
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
                        if (graceful)
                        {
                            AudioPlayer.Instance.PlayFinishedCallback();
                        }

                        if (this._waitingOneToStop)
                        {
                            this._waitOneToStop.Set();
                        }
                        // TODO signal player for next track
                    }
                }
            })
            { Priority = ThreadPriority.Highest, IsBackground = true }.Start();
        }

        public async void Disconnect()
        {
            try
            { 
                await this._client.GetGuild(this._gID).GetVoiceChannel(this._vcID).DisconnectAsync();
                this._currentAudioClient?.Dispose();
            }
            catch
            {
                // NOOP, just trying to exit gracefuly
            }

            await this._client.SetStatusAsync(UserStatus.Offline); // Sometimes may force an immediate dc
            await this._client.LogoutAsync();
            await this._client.StopAsync();
            this._client.Dispose();
        }
    }
}
