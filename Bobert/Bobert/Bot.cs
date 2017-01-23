using System;
using Discord;
using Discord.Commands;
using Discord.Audio;
using YoutubeSearch;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace Bobert
{
    class Bot
    {
        DiscordClient client;
        CommandService cmds;
        string serverName;
        string channelName;
        VideoSearch items = new VideoSearch();
        List<VideoInformation> list = new List<VideoInformation>();
        
        public Bot()
        {
            client = new DiscordClient(input =>
            {
                input.LogLevel = LogSeverity.Info;
                input.LogHandler = Log;
            });
            
            client.UsingCommands(input =>
            {
                input.PrefixChar = '\\';
                input.AllowMentionPrefix = true;
            });

            client.UsingAudio(x =>
            {
                x.Mode = AudioMode.Outgoing;
                x.Channels = 2;
            });

            cmds = client.GetService<CommandService>();

            cmds.CreateCommand("herro").Do(async (e) =>
            {
                await e.Channel.SendMessage("World!");
            });
            
            cmds.CreateGroup("play", cgb =>
            {
                string videoQuery = "";

                cgb.CreateCommand("youtube")
                        .Alias(new string[] { "yt", "y" })
                        .Description("Plays a YouTube video's audio.")
                        .Parameter("videoName", ParameterType.Required)
                        .Do( e =>
                        {
                            foreach (char element in e.GetArg("videoName"))
                            {
                                if (element == '_')
                                {
                                    videoQuery = e.GetArg("videoName").ToString().Replace('_', ' ');
                                }
                            }

                            if (!videoQuery.Contains(" "))
                            {
                                videoQuery = e.GetArg("videoName");
                            }

                            serverName = e.User.Server.Name;
                            channelName = e.User.VoiceChannel.Name;
                            
                            e.Channel.SendMessage($"{e.User.Name} played the YouTube video: {videoQuery}");
                            SendAudio("C:\\Eff.mp3");
                        });

                cgb.CreateCommand("spotify")
                        .Alias(new string[] { "sp", "s" })
                        .Description("Plays a song from Spotify.")
                        .Parameter("GreetedPerson", ParameterType.Required)
                        .Do(async e =>
                        {
                            await e.Channel.SendMessage($"{e.User.Name} says goodbye to {e.GetArg("GreetedPerson")}");
                        });
            });

            client.ExecuteAndWait(async () => { await client.Connect("MjcxNjk3OTA1NzIxNTQwNjA5.C2KYcA.wDKEh-OWHTw0XazldDs_dYniMSA", TokenType.Bot); });
        }

        private void Log(object sender, LogMessageEventArgs e)
        {
            Console.WriteLine(e.Message);
        }

        public async void SendAudio(string pathOrUrl)
        {
            var vClient = await client.GetService<AudioService>().Join(client.FindServers(serverName).FirstOrDefault().FindChannels(channelName, ChannelType.Voice, true).FirstOrDefault());

            var process = Process.Start(new ProcessStartInfo
            { // FFmpeg requires us to spawn a process and hook into its stdout, so we will create a Process
                FileName = "ffmpeg",
                Arguments = $"-i {pathOrUrl} " + // Here we provide a list of arguments to feed into FFmpeg. -i means the location of the file/URL it will read from
                            "-f s16le -ar 48000 -ac 2 pipe:1", // Next, we tell it to output 16-bit 48000Hz PCM, over 2 channels, to stdout.
                UseShellExecute = false,
                RedirectStandardOutput = true // Capture the stdout of the process
            });
            System.Threading.Thread.Sleep(2000); // Sleep for a few seconds to FFmpeg can start processing data.

            int blockSize = 3840; // The size of bytes to read per frame; 1920 for mono
            byte[] buffer = new byte[blockSize];
            int byteCount;

            while (true) // Loop forever, so data will always be read
            {
                byteCount = process.StandardOutput.BaseStream // Access the underlying MemoryStream from the stdout of FFmpeg
                        .Read(buffer, 0, blockSize); // Read stdout into the buffer

                if (byteCount == 0) // FFmpeg did not output anything
                    break; // Break out of the while(true) loop, since there was nothing to read.

                vClient.Send(buffer, 0, byteCount); // Send our data to Discord
            }
            vClient.Wait(); // Wait for the Voice Client to finish sending data, as ffMPEG may have already finished buffering out a song, and it is unsafe to return now.
            await vClient.Disconnect();
        }
    }
}
