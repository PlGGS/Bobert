using System;
using Discord;
using Discord.Commands;
using Discord.Audio;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Globalization;
using System.Reflection;
using System.Collections.Generic;
using System.Timers;

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
        static string logFileLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().GetName().CodeBase).Substring(6) + "\\log.txt";
        string audioQuery = "";
        string[] allFiles = Directory.GetFiles(audioPath); //Full path to files with name and file type
        string[] fileNames = Directory.GetFiles(audioPath); //file name
        string[] fileTypes = Directory.GetFiles(audioPath); //file type
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
        List<string> queue = new List<string>(); //TODO add audioQueue
        TicTacToe game;
        Timer timReady = new Timer(30000);
        bool gameInProgress = false;

        public Bot()
        {
            client = new DiscordClient(input =>
            {
                input.LogLevel = LogSeverity.Info;
                SetArrayValues();

                if (File.ReadAllText(logFileLocation) == "")
                {
                    File.AppendAllText(logFileLocation, $"  <<< Bobert the Incredible Bot! | {DateTime.UtcNow} >>>");
                }
                else
                {
                    File.AppendAllText(logFileLocation, $"\n  <<< Bobert the Incredible Bot! | {DateTime.UtcNow} >>>");
                }
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

            client.Log.Message += (s, e) => LogBot(s, e);
            client.MessageReceived += (s, e) => LogChat(s, e);

            void LogBot(object sender, LogMessageEventArgs e)
            {
                File.AppendAllText(logFileLocation, $"\n[{DateTime.UtcNow}] | [{e.Severity}] | {e.Source}: {e.Message}");
            }

            void LogChat(object sender, MessageEventArgs e)
            {
                File.AppendAllText(logFileLocation, $"\n[{DateTime.UtcNow}] | [{e.Channel}] | {e.Message}");
            }

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
                        .Do( e =>
                        {
                            if (audioPlaying == false)
                            {
                                loop = false;
                                audioQuery = e.GetArg("fileName");
                                PlayAudio(e);
                            }
                            else
                            {
                                e.Channel.SendMessage($"Sorry {e.User.Mention}, You have to stop the current audio before playing another song");
                            }
                        });

            cmds.CreateCommand("loop")
                        .Alias(new string[] { "l" })
                        .Description("Loops a file's audio from Pinhead's DropBox directory until someone uses the /stop command.")
                        .Parameter("fileName", ParameterType.Required)
                        .Do( e =>
                        {
                            loop = true;
                            audioQuery = e.GetArg("fileName");
                            PlayAudio(e);
                        });

            cmds.CreateCommand("stop").Alias(new string[] { "skip" })
                        .Do(async (e) =>
            {
                await e.Channel.SendMessage("audioPlaying: " + audioPlaying + " | " + "audioQuery: " + audioQuery);
                if (audioPlaying && audioQuery != "random")
                {
                    await e.Channel.SendMessage($"{e.User.Mention} stopped: {currentAudio}");
                    audioPlaying = false;
                    loop = false;
                }
                else if (audioPlaying && audioQuery == "random")
                {
                    await e.Channel.SendMessage($"{e.User.Mention} stopped the random playback of {fileNames[rnd]}");
                    audioPlaying = false;
                    loop = false;
                }
                else
                {
                    await e.Channel.SendFile($"{e.User.Mention} tried to stop current audio. Nothing was playing...");
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

            cmds.CreateCommand("game")
                        .Parameter("game", ParameterType.Required)
                        .Parameter("otherPlayer", ParameterType.Required)
                        .Do(e =>
                        {
                            if (gameInProgress == false)
                            {
                                if (e.GetArg("game") == "tic" ||
                                   e.GetArg("game") == "tac" ||
                                   e.GetArg("game") == "toe")
                                {
                                    game = new TicTacToe(client, cmds);
                                    e.Channel.SendMessage($"@{e.GetArg("otherPlayer")}, if you wish to play tic-tac-toe with {e.User.Mention}, enter 'tic {e.User.Name} ready'");
                                    timReady.Start();
                                    timReady.Elapsed += new ElapsedEventHandler(timReady_Tick);
                                    void timReady_Tick(object sender, ElapsedEventArgs newE)
                                    {
                                        e.Channel.SendMessage($"@{e.GetArg("otherPlayer")} failed to prepare for the game in time. Disposing of the new game object");
                                    }
                                    gameInProgress = true;
                                }
                            }
                        });

            cmds.CreateCommand("tic").Alias(new string[] { "tac", "toe" })
                        .Parameter("inviter", ParameterType.Required)
                        .Parameter("action", ParameterType.Required)
                        .Do(e =>
                        {
                            if (e.GetArg("action") == "ready")
                            {
                                game.Start(e.GetArg("inviter"), e.User.Name, e);
                            }
                        });

            ConnectBot();
        }

        private async void PlayAudio(CommandEventArgs e)
        {
            if (audioQuery == "random")
                rnd = randomize.Next(0, allFiles.Length);

            SetArrayValues();
            int fileTypeIndex = GetFileTypeNum(audioQuery);

            serverName = e.User.Server.Name;

            try
            {
                channelName = e.User.VoiceChannel.Name;

                if (fileTypeIndex != -1)
                {
                    audioPlaying = true;
                    currentAudio = audioQuery;
                    await e.Channel.SendMessage($"{e.User.Mention} played: {audioQuery}");
                    SendAudio(audioPath + audioQuery + fileTypes[fileTypeIndex], e);
                }
                else if (audioQuery == "random")
                {
                    audioPlaying = true;
                    currentAudio = audioQuery;
                    await e.Channel.SendMessage($"{e.User.Mention} played a random audio file ({fileNames[rnd]})");
                    SendAudio(audioPath + fileNames[rnd] + fileTypes[rnd], e);
                }
                else if (audioQuery != "random")
                {
                    await e.Channel.SendMessage($"Sadly, {e.User.Mention} tried to play an audio file that doesn't exist. ({audioQuery}) Use /listFiles for a list of items to play");
                }
            }
            catch (NullReferenceException)
            {
                await e.Channel.SendMessage($"Sadly, {e.User.Mention} tried to play an audio file while not in a voice channel");
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
