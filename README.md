# SLT Music Bot

This is a small WPF application that is designed to create a discord bot to play music on a discord server. It uses [Discord.Net](https://github.com/discord-net/Discord.Net) for the web backend and WPF for the frontend.

## Building
To build this application you will need the following:

* [.net 6](https://dotnet.microsoft.com/download/dotnet/6.0) - just needs to be installed.
* A discord bot token as a system variable by the name SLTPrivateMusicBotToken - see [discord dev portal](https://discord.com/developers/docs/intro) for more info on bot tokens
* [FFMpeg compiled binary](https://ffmpeg.org/download.html) in your output directory - FFMpeg is used to convert any user-inserted data to PCM 48000 sr audio data.
* [FFProbe compiled binary](http://www.ffmpeg.org/download.html) in your output directory - FFProbe is used to get the audio metadata without parsing the whole file.
* [libopus and libsodium dynamic link libraries](https://dsharpplus.github.io/natives/index.html) in your output directory - required by Discord.Net. !IMPORTANT! libopus.dll must be renamed to opus.dll for Discord.Net to work
* [youtube-dl compiled binary in your output directory](https://github.com/ytdl-org/youtube-dl/releases) **optional** - allows the bot to download youtube videos and play them as music.

## Usage
* Using the discord bot creation tool invite the bot to your server. In theory it only needs permissions to read messages/roles, join voice channels and stream audio. However in practice usually an admin role will be needed. Not really sure why, but otherwise discord's api will return a permission error on any operation.
* Start the executable and press connect. Your bot should go online.
* Make an administrator of the server ping the bot in any channel while they are in a voice channel. The bot should join the channel.
* Drag and drop your media files into the bot application window. It will process them and put them into the playlist.
* **optional** if youtube-dl is installed you can drag and drop a youtube link into the application aswell, though downloading videos takes a considerable amount of time.
* Play your audio using the controls.

## Disclaimer
This is a hobby project and was not really intended for a real usage outside of the debug mode in an IDE. The repository here on github exists for educational purposes mostly. 
The bot is also not well-optimized, storing the PCM in ram (only the currently playing one).
