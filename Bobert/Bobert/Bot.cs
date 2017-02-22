using System;
using Discord;
using Discord.Commands;
using Discord.Audio;
using YoutubeSearch;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Collections;

namespace Bobert
{
    class Bot
    {
        DiscordClient client;
        CommandService cmds;
        string serverName;
        string channelName;
        static string audioPath = @"C:\Pinhead\Dropbox\Public\Audio\";
        string audioQuery;
        string[] audioFiles = Directory.GetFiles(audioPath); //Full path to files
        string[] fileNames = Directory.GetFiles(audioPath); //file name
        string[] fileTypes = Directory.GetFiles(audioPath); //file type


        public Bot()
        {
            client = new DiscordClient(input =>
            {
                input.LogLevel = LogSeverity.Info;
                input.LogHandler = Log;
            });
            
            client.UsingCommands(input =>
            {
                input.PrefixChar = '/';
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

            cmds.CreateCommand("play")
                        .Alias(new string[] { "pl", "p" })
                        .Description("Plays a file's audio from Pinhead's DropBox directory.")
                        .Parameter("fileName", ParameterType.Required)
                        .Do(e =>
                        {
                            foreach (char element in e.GetArg("fileName"))
                            {
                                if (element == '_')
                                {
                                    audioQuery = e.GetArg("fileName").ToString().Replace('_', ' ');
                                }
                            }

                            audioQuery = e.GetArg("fileName").ToString();

                            for (int i = 0; i < Directory.GetFiles(audioPath).Length; i++)
                            {
                                audioFiles = Directory.GetFiles(audioPath);
                                fileNames = Directory.GetFiles(audioPath);
                                fileTypes = Directory.GetFiles(audioPath);

                                fileNames[i] = audioFiles[i].Substring(audioPath.Length, audioFiles[i].Substring(audioPath.Length - 1).Length - 1);
                                fileTypes[i] = audioFiles[i].Substring(audioPath.Length + fileNames[i].Length - 4, 4);
                                fileNames[i] = fileNames[i].Substring(0, fileNames[i].Length - 4);

                                e.Channel.SendMessage(audioFiles[i]);
                                e.Channel.SendMessage(fileNames[i]);
                                e.Channel.SendMessage(fileTypes[i]);
                            }

                            int fileTypeIndex = -1;

                            for (int i = 0; i < fileNames.Length; i++)
                            {
                                if (fileNames[i] == e.GetArg("fileName"))
                                {
                                    fileTypeIndex = i;
                                }

                            }

                            if (!audioQuery.Contains(" "))
                            {
                                audioQuery = e.GetArg("fileName");
                            }
                            
                            serverName = e.User.Server.Name;
                            channelName = e.User.VoiceChannel.Name;

                            e.Channel.SendMessage($"{e.User.Name} played: {audioQuery}");
                            //SendAudio(videoSearchItems.SearchQuery(e.GetArg("videoName"), 1)[0].Url); //OLD WAY OF FINDING YOUTUBE VIDEOS

                            SendAudio(audioPath + e.GetArg("fileName") + fileTypes[fileTypeIndex]);

                            //TODO send the audio of the song that is searched for with the proper file ending
                            //TODO add a command that allows the user to view a list of all playable songs
                            //TODO add a command that allows the user to change the volume of the bot
                            //TODO install dropbox and teamviewer on the $8 PC for use of Pinhead on there
                            //TODO remove yt and spotify parts of commands and maybe remove youtubesearch.dll from references

                        });

            cmds.CreateCommand("spotify")
                        .Alias(new string[] { "sp", "s" })
                        .Description("Plays a song from Spotify.")
                        .Parameter("songName", ParameterType.Required)
                        .Do(e =>
                        {
                            
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
