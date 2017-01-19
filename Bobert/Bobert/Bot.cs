using System;
using Discord;
using Discord.Commands;
using Discord.Audio;

namespace Bobert
{
    class Bot
    {
        DiscordClient client;
        CommandService cmds;
        char prefix = '\u005c';

        public Bot()
        {
            client = new DiscordClient(input =>
            {
                input.LogLevel = LogSeverity.Info;
                input.LogHandler = Log;
            });
            
            client.UsingCommands(input =>
            {
                input.PrefixChar = prefix;
                input.AllowMentionPrefix = true;
            });
            
            cmds = client.GetService<CommandService>();

            cmds.CreateCommand("herro").Do(async (e) =>
            {
                await e.Channel.SendMessage("World!");
            });

            client.ExecuteAndWait(async () => { await client.Connect("G70XExJESX0TEREgYw1ynPVtTwv08g2V", TokenType.Bot); });
        }

        private void Log(object sender, LogMessageEventArgs e)
        {
            Console.WriteLine(e.Message);
        }
    }
}
