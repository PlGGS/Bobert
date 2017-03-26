using Discord;
using Discord.Commands;
using System;

namespace Bobert
{
    class TicTacToe
    {
        DiscordClient client;
        CommandService cmds;
        string[,] board = new string[,]
        {
            {" ", " ", " "},
            {" ", " ", " "},
            {" ", " ", " "}
        };
        bool xTurn;
        string inviter, accepter;
        
        public TicTacToe(DiscordClient myClient, CommandService myCmds)
        {
            client = myClient;
            cmds = myCmds;
            
            cmds.CreateCommand("place")
                        .Parameter("x", ParameterType.Required)
                        .Parameter("y", ParameterType.Required)
                        .Do(e =>
                        {
                            board[Int32.Parse(e.GetArg("x")), Int32.Parse(e.GetArg("y"))] = "X";
                            DrawBoard(e);
                        });
        }

        public void Start(string gameInviter, string gameAccepter, CommandEventArgs e)
        {
            inviter = e.GetArg("inviter");
            accepter = e.User.Name;
            e.Channel.SendMessage("Welcome to Tic-Tac-Toe! " +
                "To play, simply enter the command 'place', and then enter the coordinates of where you would like to place your peice");
            //DrawBoard(e);
            for (int i = 0; i > 3; i++)
            {
                e.Channel.SendMessage($"{board[i, 0].ToString()}   {board[i, 1].ToString()}   {board[i, 2].ToString()}");
            }
        }
        
        public void DrawBoard(CommandEventArgs e)
        {
            for (int i = 0; i > board.Length; i++)
            {
                e.Channel.SendMessage("fuck");
                e.Channel.SendMessage($"{board[i, 0].ToString()}   {board[i, 1].ToString()}   {board[i, 2].ToString()}");
            }
        }
    }
}
