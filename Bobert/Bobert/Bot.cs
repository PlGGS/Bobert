using System;
using Discord;
using Discord.Commands;
using Discord.Audio;
using YoutubeSearch;
using System.Linq;
using System.Diagnostics;
using System.IO;

namespace Bobert
{
    class Bot
    {
        DiscordClient client;
        CommandService cmds;
        string serverName;
        string channelName;
        IAudioClient vClient;
        Process procFFMPEG;
        static string audioPath = @"C:\Pinhead\Dropbox\Public\Audio\";
        string audioQuery;
        string[] audioFiles = Directory.GetFiles(audioPath); //Full path to files
        string[] fileNames = Directory.GetFiles(audioPath); //file name
        string[] fileTypes = Directory.GetFiles(audioPath); //file type
        string[] commands = new string[] {"play", "stop", "listFiles", "help"};
        bool audioPlaying = false;
        string currentAudio = "";

        public Bot()
        {
            client = new DiscordClient(input =>
            {
                input.LogLevel = LogSeverity.Info;
                input.LogHandler = Log;
                SetArrayValues();
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
            
            cmds.CreateCommand("listFiles").Do(async (e) =>
            {
                await e.Channel.SendMessage("To add more files, go to https://www.dropbox.com/sh/8vy5iz7ndsgcnpl/AAA6yI_TcR_swccegTeTpcqfa?dl=0, and drop in your own (MP3 and WAV files only, NO spaces in the file names)");
                await e.Channel.SendMessage("Files:");

                for (int i = 0; i < fileNames.Length; i++)
                {
                    await e.Channel.SendMessage(fileNames[i]);
                }
            });

            cmds.CreateCommand("help").Do(async (e) =>
            {
                await e.Channel.SendMessage("Commands:");

                foreach (string cmd in commands)
                {
                    await e.Channel.SendMessage(cmd);
                }
            });

            cmds.CreateCommand("vol").Do(async (e) =>
            {
                await e.Channel.SendMessage("");

                if (audioPlaying)
                {
                    //TODO figure out if it is possible to change the volume of the output stream
                }
                else
                {
                    //TODO decide whether or not a different output occurs if audio is not currently playing
                }
            });

            cmds.CreateCommand("play")
                        .Alias(new string[] { "pl", "p" })
                        .Description("Plays a file's audio from Pinhead's DropBox directory.")
                        .Parameter("fileName", ParameterType.Required)
                        .Do(e =>
                        {
                            if (audioPlaying)
                            {
                                e.Channel.SendMessage("Audio is already playing. Use /stop to end current playback");
                            }
                            else
                            {
                                audioPlaying = true;
                                currentAudio = e.GetArg("fileName");

                                foreach (char element in e.GetArg("fileName"))
                                {
                                    if (element == '_')
                                    {
                                        audioQuery = e.GetArg("fileName").ToString().Replace('_', ' ');
                                    }
                                }

                                audioQuery = e.GetArg("fileName").ToString();
                                SetArrayValues();

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

                                if (fileTypeIndex != -1)
                                {
                                    e.Channel.SendMessage($"{e.User.Name} played: {audioQuery}");
                                    SendAudio(audioPath + e.GetArg("fileName") + fileTypes[fileTypeIndex]);
                                }
                                else
                                {
                                    e.Channel.SendMessage($"Sadly, {e.User.Name} tried to play an audio file that doesn't exist. Use /listFiles for a list of items to play");
                                }

                                //SendAudio(videoSearchItems.SearchQuery(e.GetArg("videoName"), 1)[0].Url); //OLD WAY OF FINDING YOUTUBE VIDEOS
                                //TODO possibly remove youtubesearch.dll from references if it's just not gonna happen
                            }
                        });

            cmds.CreateCommand("stop").Do(async (e) =>
            {
                if (audioPlaying)
                {
                    await e.Channel.SendMessage($"{e.User.Name} stopped: {currentAudio}");
                    audioPlaying = false;
                } else
                {
                    await e.Channel.SendFile($"{e.User.Name} tried to stop current audio. Nothing was playing...");
                }
            });
            
            client.ExecuteAndWait(async () => { await client.Connect("MjcxNjk3OTA1NzIxNTQwNjA5.C2KYcA.wDKEh-OWHTw0XazldDs_dYniMSA", TokenType.Bot); });
        }

        private void SetArrayValues()
        {
            audioFiles = Directory.GetFiles(audioPath);
            fileNames = Directory.GetFiles(audioPath);
            fileTypes = Directory.GetFiles(audioPath);

            for (int i = 0; i < Directory.GetFiles(audioPath).Length; i++)
            {
                fileNames[i] = audioFiles[i].Substring(audioPath.Length, audioFiles[i].Substring(audioPath.Length - 1).Length - 1);
                fileTypes[i] = audioFiles[i].Substring(audioPath.Length + fileNames[i].Length - 4, 4);
                fileNames[i] = fileNames[i].Substring(0, fileNames[i].Length - 4);
            }
        }

        private void Log(object sender, LogMessageEventArgs e)
        {
            Console.WriteLine(e.Message);
        }

        public async void SendAudio(string pathOrUrl)
        {
            //TODO make sure this works as a public variable
            vClient = await client.GetService<AudioService>().Join(client.FindServers(serverName).FirstOrDefault().FindChannels(channelName, ChannelType.Voice, true).FirstOrDefault());

            procFFMPEG = Process.Start(new ProcessStartInfo
            { // FFmpeg requires us to spawn a process and hook into its stdout, so we will create a Process
                FileName = "ffmpeg",
                Arguments = $"-i {pathOrUrl} " + // Here we provide a list of arguments to feed into FFmpeg. -i means the location of the file/URL it will read from
                            "-f s16le -ar 48000 -ac 2 pipe:1", // Next, we tell it to output 16-bit 48000Hz PCM, over 2 channels, to stdout.
                UseShellExecute = false,
                RedirectStandardOutput = true // Capture the stdout of the process
            });

            procFFMPEG.PriorityBoostEnabled = true;
            System.Threading.Thread.Sleep(2000); // Sleep for a few seconds to FFmpeg can start processing data.

            int blockSize = 3840; // The size of bytes to read per frame; 1920 for mono
            byte[] buffer = new byte[blockSize];
            int byteCount;

            while (audioPlaying) // TODO check to see if user stops the audio playback
            {
                byteCount = procFFMPEG.StandardOutput.BaseStream // Access the underlying MemoryStream from the stdout of FFmpeg
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
