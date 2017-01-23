using System;
using Discord;
using Discord.Commands;
using Discord.Audio;
using YoutubeSearch;
using System.Collections.Generic;
using System.Linq;

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

                            var voiceChannel = client.FindServers(e.User.VoiceChannel.ToString()).FirstOrDefault().VoiceChannels.FirstOrDefault();
                            var _vClient = client.GetService<AudioService>().Join(voiceChannel);

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
    }
}
