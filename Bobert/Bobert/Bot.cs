using System;
using Discord;
using Discord.Commands;
using Discord.Audio;
using YoutubeSearch;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;

namespace Bobert
{
    class Bot
    {
        DiscordClient client;
        CommandService cmds;
        IAudioClient vClient;


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

            cmds = client.GetService<CommandService>();

            client.UsingAudio(x =>
            {
                x.Mode = AudioMode.Outgoing;
            });
            
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
                        .Do(async e =>
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
                            
                            await e.Channel.SendMessage($"{e.User.Name} played the YouTube video: {videoQuery}");
                            var _vClient = client.GetService<AudioService>().Join(client.FindServers(e.User.VoiceChannel.ToString()).FirstOrDefault().VoiceChannels.FirstOrDefault());
                            
                            //play audio n' shit
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

        public void SendAudio(string pathOrUrl)
        {
            var process = Process.Start(new ProcessStartInfo
            { // FFmpeg requires us to spawn a process and hook into its stdout, so we will create a Process
                FileName = "ffmpeg",
                Arguments = $"-i {pathOrUrl} " + // Here we provide a list of arguments to feed into FFmpeg. -i means the location of the file/URL it will read from
                            "-f s16le -ar 48000 -ac 2 pipe:1", // Next, we tell it to output 16-bit 48000Hz PCM, over 2 channels, to stdout.
                UseShellExecute = false,
                RedirectStandardOutput = true // Capture the stdout of the process
            });
            Thread.Sleep(2000); // Sleep for a few seconds to FFmpeg can start processing data.

            int blockSize = 3840; // The size of bytes to read per frame; 1920 for mono
            byte[] buffer = new byte[blockSize];
            int byteCount;

            while (true) // Loop forever, so data will always be read
            {
                byteCount = process.StandardOutput.BaseStream // Access the underlying MemoryStream from the stdout of FFmpeg
                        .Read(buffer, 0, blockSize); // Read stdout into the buffer

                if (byteCount == 0) // FFmpeg did not output anything
                    break; // Break out of the while(true) loop, since there was nothing to read.

                audioClient.Send(buffer, 0, byteCount); // Send our data to Discord
            }
            IAudioClient.Wait(); // Wait for the Voice Client to finish sending data, as ffMPEG may have already finished buffering out a song, and it is unsafe to return now.
        }
    }
}
