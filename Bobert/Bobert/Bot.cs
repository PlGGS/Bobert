﻿using System;
using System.Collections;
using Discord;
using Discord.Commands;
using Discord.Audio;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Threading.Tasks;

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
        string nextFileInQueue = "";
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
                            await e.Channel.SendMessage($"{e.User.Mention} changed the volume to: {e.GetArg("amt")}");
                            await procFFMPEG.StandardInput.WriteLineAsync($"ffmpeg -f lavfi -i \"amovie = {currentAudio + fileTypes[GetFileTypeNum(currentAudio)]}, volume = { e.GetArg("amt")}\" {currentAudio + fileTypes[GetFileTypeNum(currentAudio)]}");
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
                            PlayAudio(true, e);
                        });

            cmds.CreateCommand("loop")
                        .Alias(new string[] { "l" })
                        .Description("Loops a file's audio from Pinhead's DropBox directory until someone uses the /stop command.")
                        .Parameter("fileName", ParameterType.Required)
                        .Do(e =>
                        {
                            loop = true;
                            PlayAudio(true, e);
                        });

            cmds.CreateCommand("stop").Alias(new string[] { "skip" })
                        .Do(async (e) =>
            {
                await e.Channel.SendMessage("audioPlaying: " + audioPlaying + " | " + "audioQuery: " + audioQuery);

                if (audioPlaying == false)
                {
                    await e.Channel.SendMessage($"{e.User.Mention} tried to stop current audio. Nothing was playing...");
                }
                else if (audioPlaying && audioQuery != "random")
                {
                    if (audioQuery == "random")
                    {
                        await e.Channel.SendMessage($"{e.User.Mention} stopped the random playback of {fileNames[rnd]}");
                    }
                    else
                    {
                        await e.Channel.SendMessage($"{e.User.Mention} stopped: {currentAudio}");
                    }

                    //TODO add for loop to place audioQueue.ToArray()[1] as audioQueue.ToArray()[0] and so on whilst deleting the old audioQueue.ToArray()[0]
                    audioPlaying = false;
                    loop = false;
                    audioQueue.ToArray()[0] = audioQuery;
                }
                else if (audioPlaying && audioQuery == "random")
                {
                    audioQueue.ToArray()[0] = audioQuery;
                    if (audioQueue.Count >= 1)
                    {
                        audioPlaying = false;
                        PlayNextInQueue(e);
                    }
                    else
                    {
                        audioPlaying = false;
                        loop = false;
                    }
                }
            });

            cmds.CreateCommand("volume").Alias(new string[] { "vol" })
                        .Parameter("volPercent", ParameterType.Required)
                        .Do(async (e) =>
                        {
                            Process tmpProc = Process.GetProcessesByName("ffmpeg").First();
                            await e.Channel.SendMessage($"{e.User.Mention} set the volume to {e.GetArg("volPercent")}");
                            await VolumeMixer.SetApplicationVolume(tmpProc.Id, float.Parse(e.GetArg("volPercent"), CultureInfo.InvariantCulture.NumberFormat));
                            //TODO fix this...?
                        });
            
            ConnectBot();
        }

        async void PlayNextInQueue(CommandEventArgs e)
        {
            nextFileInQueue = audioQueue.ToArray()[0].ToString();
            await e.Channel.SendMessage($"Playing: {nextFileInQueue}. {audioQueue.Count - 1} songs left in queue");
            PlayAudio(false, e);
        }

        private async void PlayAudio(bool getArg, CommandEventArgs e)
        {
            if (getArg)
            {
                audioQuery = e.GetArg("fileName").ToString();
            }
            else
            {
                audioQuery = nextFileInQueue;
            }
            
            if (audioQuery == "random")
                rnd = randomize.Next(0, allFiles.Length);
            
            if ((audioPlaying == false && getArg) || (audioPlaying && getArg == false))
            {
                SetArrayValues();
                int fileTypeIndex = GetFileTypeNum(audioQuery);

                serverName = e.User.Server.Name;

                if (getArg)
                {
                    try
                    {
                        channelName = e.User.VoiceChannel.Name;
                    }
                    catch (NullReferenceException)
                    {
                        await e.Channel.SendMessage("Please join a voice channel before attempting to play audio");
                    }
                }

                if (fileTypeIndex != -1)
                {
                    audioPlaying = true;
                    currentAudio = audioQuery;
                    if (getArg == true)
                        await e.Channel.SendMessage($"{e.User.Mention} played: {audioQuery}");
                    if (getArg == false)
                        audioQueue.RemoveAt(0);
                    SendAudio(audioPath + audioQuery + fileTypes[fileTypeIndex], e);
                }
                else if (audioQuery == "random")
                {
                    audioPlaying = true;
                    currentAudio = audioQuery;
                    if (getArg == true)
                        await e.Channel.SendMessage($"{e.User.Mention} played a random audio file ({fileNames[rnd]})");
                    if (getArg == false)
                        audioQueue.RemoveAt(0);
                    SendAudio(audioPath + fileNames[rnd] + fileTypes[rnd], e);
                }
                else if (audioQuery != "random")
                {
                    if (getArg == false)
                        audioQueue.RemoveAt(0);
                    await e.Channel.SendMessage($"Sadly, {e.User.Mention} tried to play an audio file that doesn't exist. ({audioQuery}) Use /listFiles for a list of items to play");
                }
                else
                {
                    await e.Channel.SendMessage("fileTypeIndex: " + fileTypeIndex);
                }

                //SendAudio(videoSearchItems.SearchQuery(e.GetArg("videoName"), 1)[0].Url); //OLD WAY OF FINDING YOUTUBE VIDEOS
            }
            else if (audioPlaying && getArg)
            {
                audioQueue.Add(audioQuery);
                await e.Channel.SendMessage($"{e.User.Mention} added {audioQuery} to the queue");
            }
        }
        
        private int GetFileTypeNum(string fileName)
        {
            int fileTypeIndex = -1;

            for (int i = 0; i < fileNames.Length; i++)
            {
                if (fileNames[i] == fileName)
                {
                    fileTypeIndex = i;
                }
            }

            return fileTypeIndex;
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

            //TODO create a proper logging system for debugging
        }

        public async void SendAudio(string pathOrUrl, CommandEventArgs e)
        {
            try
            {
                vClient = await client.GetService<AudioService>().Join(client.FindServers(serverName).FirstOrDefault().FindChannels(channelName, ChannelType.Voice, true).FirstOrDefault());

                await e.Channel.SendMessage(pathOrUrl);
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
                    await vClient.Disconnect();
                    vClient.Wait();
                    audioPlaying = false;
                    //TODO figure out why you can play a song that doesn't exist after a song is already playing
                    PlayNextInQueue(e);
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
                SendAudio(currentAudio, e);
            }
        }
    }
}
