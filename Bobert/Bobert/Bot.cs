using System;
using System.Collections;
using Discord;
using Discord.Commands;
using Discord.Audio;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Globalization;

namespace Bobert
{
    class Bot
    {
        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string strClassName, string strWindowName);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        DiscordClient client;
        CommandService cmds;
        string serverName = "";
        string channelName = "";
        IAudioClient vClient;
        Process procFFMPEG;
        static string audioPath = @"C:\Pinhead\Dropbox\Public\Audio\";
        string audioQuery = "";
        string[] allFiles = Directory.GetFiles(audioPath); //Full path to files with name and file type
        string[] fileNames = Directory.GetFiles(audioPath); //file name
        string[] fileTypes = Directory.GetFiles(audioPath); //file type
        string[] audioFiles; //TODO make an array of all audioFiles that is the correct length
        string[] commands = new string[] {"play [pl, p]: Plays a specified audio file one time",
                                          "loop [l]: Repeatedly plays a specified audio file",
                                          "stop [skip]: Stops a currently playing audio file",
                                          "volume [vol, v]: Allows users to set the volume of the bot to a value between 0 and 100",
                                          "listFiles [files, flies]: Lists all files in the bot's audio folder",
                                          "help [h]: Shows this list of commands" };
        static Random randomize = new Random();
        int rnd = 0;
        bool audioPlaying = false;
        bool loop = false;
        string currentAudio = "";
        string currentFile = "";
        ArrayList audioQueue = new ArrayList();

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

            cmds.CreateCommand("help")
                        .Alias(new string[] {"h"})
                        .Do(async (e) =>
            {
                await e.Channel.SendMessage("Commands:");

                foreach (string cmd in commands)
                {
                    await e.Channel.SendMessage(cmd);
                }
            });

            cmds.CreateCommand("listFiles")
                .Alias(new string[] { "files", "flies" })
                .Do(async (e) =>
            {
                await e.Channel.SendMessage("To add more files, go to https://www.dropbox.com/sh/8vy5iz7ndsgcnpl/AAA6yI_TcR_swccegTeTpcqfa?dl=0, and drop in your own (MP3 and WAV files only, NO spaces in the file names)");
                await e.Channel.SendMessage("Files:");
                string tmpList = "";

                for (int i = 0; i < fileNames.Length; i++)
                {
                    if (!fileNames[i].Contains(audioPath))
                    {
                        tmpList += "- " + fileNames[i] + "\n";
                    }
                }

                await e.Channel.SendMessage(tmpList);
            });

            cmds.CreateCommand("volume")
                        .Alias(new string[] {"vol", "v"})
                        .Parameter("amt", ParameterType.Required)
                        .Do(async (e) =>
                        {
                            await e.Channel.SendMessage($"{e.User.Name} changed the volume to: {e.GetArg("amt")}");
                            await procFFMPEG.StandardInput.WriteLineAsync($"ffmpeg -f lavfi -i \"amovie = {currentFile}, volume = { e.GetArg("amt")}\" {currentFile}");
                            await e.Channel.SendMessage("Command sent without error");

                            if (audioPlaying)
                            {
                                //TODO figure out if it is possible to change the volume of the output stream
                            }
                            else
                            {
                                //TODO figure out whether or not a different output occurs before a song is actually played
                                //TODO if not, just save the value for using the command when a user does play a song
                            }
                        });

            cmds.CreateCommand("play")
                        .Alias(new string[] { "pl", "p" })
                        .Description("Plays a file's audio from Pinhead's DropBox directory.")
                        .Parameter("fileName", ParameterType.Required)
                        .Do(e =>
                        {
                            loop = false;
                            PlayAudio(e);
                        });

            cmds.CreateCommand("loop")
                        .Alias(new string[] { "l" })
                        .Description("Loops a file's audio from Pinhead's DropBox directory until someone uses the /stop command.")
                        .Parameter("fileName", ParameterType.Required)
                        .Do(e =>
                        {
                            loop = true;
                            PlayAudio(e);
                        });

            cmds.CreateCommand("stop").Alias(new string[] { "skip" })
                        .Do(async (e) =>
            {
                if (audioPlaying && audioQuery != "random")
                {
                    await e.Channel.SendMessage($"{e.User.Name} stopped: {currentAudio}");
                    audioPlaying = false;
                    loop = false;
                    playNextInQueue();
                }
                else if (audioPlaying && audioQuery == "random")
                {
                    await e.Channel.SendMessage($"{e.User.Name} stopped the random playback of {fileNames[rnd]}");
                    audioPlaying = false;
                    loop = false;
                    playNextInQueue();
                }
                else
                {
                    await e.Channel.SendFile($"{e.User.Name} tried to stop current audio. Nothing was playing...");
                }

                async void playNextInQueue()
                {
                    if (audioQueue.Count > 0)
                    {
                        await e.Channel.SendMessage($"Playing: {audioQueue.ToArray()[0]}. {audioQueue.Count - 1} songs left in queue");
                        SendAudio(audioPath + audioQueue.ToArray()[0], e);
                        audioQueue.RemoveAt(0);
                    }
                }
            });

            cmds.CreateCommand("volume").Alias(new string[] { "vol" })
                        .Parameter("volPercent", ParameterType.Required)
                        .Do(async (e) =>
                        {
                            Process tmpProc = Process.GetProcessesByName("ffmpeg").First();

                            await e.Channel.SendMessage($"{e.User.Name} set the volume to {e.GetArg("volPercent")}");
                            await VolumeMixer.SetApplicationVolume(tmpProc.Id, float.Parse(e.GetArg("volPercent"), CultureInfo.InvariantCulture.NumberFormat));
                            //TODO fix this...?
                        });
            
            ConnectBot();
        }

        private void PlayAudio(CommandEventArgs e)
        {
            audioQuery = e.GetArg("fileName").ToString();

            //TODO check if this if statement breaks random audio playback
            if (audioQuery == "random")
                rnd = randomize.Next(0, allFiles.Length);

            if (audioPlaying)
            {
                audioQueue.Add(audioQuery);
                e.Channel.SendMessage($"{e.User.Name} added {audioQuery} to the queue");
            }
            else
            {
                foreach (char element in e.GetArg("fileName"))
                {
                    if (element == '_')
                    {
                        audioQuery = e.GetArg("fileName").ToString().Replace('_', ' ');
                    }
                }
                
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
                else
                {
                    e.Channel.SendMessage("Please make sure there are no spaces in the file name you wish to play");
                }

                serverName = e.User.Server.Name;

                try
                {
                    channelName = e.User.VoiceChannel.Name;
                }
                catch (NullReferenceException)
                {
                    e.Channel.SendMessage("Please join a voice channel before attempting to play audio");
                }

                if (fileTypeIndex != -1)
                {
                    audioPlaying = true;
                    currentAudio = e.GetArg("fileName");

                    e.Channel.SendMessage($"{e.User.Name} played: {audioQuery}");
                    currentFile = e.GetArg("fileName") + fileTypes[fileTypeIndex];
                    SendAudio(audioPath + e.GetArg("fileName") + fileTypes[fileTypeIndex], e);
                }
                else if (audioQuery == "random")
                {
                    audioPlaying = true;
                    currentAudio = e.GetArg("fileName");

                    e.Channel.SendMessage($"{e.User.Name} played a random audio file ({fileNames[rnd]})");
                    SendAudio(audioPath + fileNames[rnd] + fileTypes[rnd], e);
                }
                else if (audioQuery != "random")
                {
                    e.Channel.SendMessage($"Sadly, {e.User.Name} tried to play an audio file that doesn't exist. Use /listFiles for a list of items to play");
                }

                //SendAudio(videoSearchItems.SearchQuery(e.GetArg("videoName"), 1)[0].Url); //OLD WAY OF FINDING YOUTUBE VIDEOS
                //TODO add loop command
            }
        }

        private void ConnectBot()
        {
            client.ExecuteAndWait(async () => { await client.Connect("MjcxNjk3OTA1NzIxNTQwNjA5.C2KYcA.wDKEh-OWHTw0XazldDs_dYniMSA", TokenType.Bot); });
        }

        private void SetArrayValues()
        {
            allFiles = Directory.GetFiles(audioPath);
            fileNames = Directory.GetFiles(audioPath);
            fileTypes = Directory.GetFiles(audioPath);
            
            for (int i = 0; i < allFiles.Length; i++)
            {
                if (allFiles[i].Substring(allFiles[i].Length - 4, 4) == ".wav" ||
                    allFiles[i].Substring(allFiles[i].Length - 4, 4) == ".mp3")
                {
                    //audioFiles[i] = allFiles[i].Substring(audioPath.Length, allFiles[i].Substring(audioPath.Length - 1).Length - 1);
                    fileNames[i] = allFiles[i].Substring(audioPath.Length, allFiles[i].Substring(audioPath.Length - 1).Length - 1);
                    fileTypes[i] = allFiles[i].Substring(audioPath.Length + fileNames[i].Length - 4, 4);
                    fileNames[i] = fileNames[i].Substring(0, fileNames[i].Length - 4);
                }
            }
        }

        private void Log(object sender, LogMessageEventArgs e)
        {
            Console.WriteLine(e.Message);

            //if (!File.Exists(audioPath + "log.txt"))
            //{
            //    File.Create(audioPath + "log.txt");
            //}

            //StreamWriter logger = new StreamWriter(audioPath + "log.txt");
            //logger.WriteLine(e.Message);
        }

        public async void SendAudio(string pathOrUrl, CommandEventArgs e)
        {
            try
            {
                vClient = await client.GetService<AudioService>().Join(client.FindServers(serverName).FirstOrDefault().FindChannels(channelName, ChannelType.Voice, true).FirstOrDefault());

                procFFMPEG = Process.Start(new ProcessStartInfo
                { //FFmpeg requires us to spawn a process and hook into its stdout, so we will create a Process
                    FileName = "ffmpeg",
                    Arguments = $"-i {pathOrUrl} " + //Here we provide a list of arguments to feed into FFmpeg. -i means the location of the file/URL it will read from
                                "-f s16le -ar 48000 -ac 2 pipe:1", //Next, we tell it to output 16-bit 48000Hz PCM, over 2 channels, to stdout.
                    UseShellExecute = false,
                    RedirectStandardOutput = true //Capture the stdout of the process
                });

                procFFMPEG.PriorityBoostEnabled = true;
                System.Threading.Thread.Sleep(2000); //Sleep for a few seconds to FFmpeg can start processing data.

                int blockSize = 3840; //The size of bytes to read per frame; 1920 for mono
                byte[] buffer = new byte[blockSize];
                int byteCount;

                while (audioPlaying) //Check to see if user stops the audio playback
                {
                    byteCount = procFFMPEG.StandardOutput.BaseStream //Access the underlying MemoryStream from the stdout of FFmpeg
                            .Read(buffer, 0, blockSize); //Read stdout into the buffer

                    if (byteCount == 0) //FFmpeg did not output anything
                        break; //Break out of the while(true) loop, since there was nothing to read.

                    vClient.Send(buffer, 0, byteCount); //Send our data to Discord
                }
                vClient.Wait(); //Wait for the Voice Client to finish sending data, as ffMPEG may have already finished buffering out a song, and it is unsafe to return now.

                if (loop)
                {
                    SendAudio(pathOrUrl, e);
                }
                else if (audioQueue.Count > 0)
                {
                    await e.Channel.SendMessage($"Playing: {audioQueue.ToArray()[0]}. {audioQueue.Count - 1} songs left in queue");
                    SendAudio(audioPath + audioQueue.ToArray()[0], e);
                    audioQueue.RemoveAt(0);
                }
                else
                {
                    await vClient.Disconnect();
                    audioPlaying = false;
                }
            }
            catch (Exception)
            {
                ConnectBot();
                SendAudio(pathOrUrl, e);
            }
        }
    }
}
