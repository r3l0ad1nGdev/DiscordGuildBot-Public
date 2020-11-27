using System;
using DSharpPlus;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace GuildBot
{
    static class Initilization
    {
        static void Main(string[] args)
        {
            MainTask(args).ConfigureAwait(false).GetAwaiter().GetResult();
            Console.ReadLine();
        }
        static async Task MainTask(string[] args)
        {
            var discord = new CustomDiscordClient(new DiscordConfiguration
            {
                //Add bot token here
                Token = "NzYzNzUzNDY5MjQwNTQxMjE1.X38S2A.vq1DR2_5UhZM2A5Gid__MZKH3H0",
                TokenType = TokenType.Bot,
                UseInternalLogHandler = true,
                LogLevel = LogLevel.Debug

            });

            discord.Ready += async data =>
            {
                var botEnv = new BotEnvironment();

                if (args.Length != 1)
                {
                    Console.Error.WriteLine("ERROR: You must load a bot config file in order to process reactions.");
                }
                if (botEnv.JoinChannelId == 0)
                {
                    Console.Error.WriteLine("Please set the Join Channel ID (where the join message is sent) in BotEnvironment.cs");
                }
                else
                {
                    try
                    {
                        var botConfig = BotConfigBuilder.Build(File.ReadAllText(args[0]));
                        var messageId = await discord.CreateJoinMessage(botConfig);
                        discord.AddMessageReactionAddedHandler(new BotReactionsStateMachine(discord, botConfig, messageId).ProcessMessageReaction);
                        Console.WriteLine($"Configuration {Path.GetFileNameWithoutExtension(args[0])} loaded...");
                        Console.WriteLine("Bot ready...");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine(ex.Message);
                    }
                }
            };
            discord.MessageCreated += new BotMessageProcessor(discord).OnMessageCreated;
            //discord.MessageCreated += new BotMessageProcessor(discord).MessageCommands;

            discord.MessageCreated += async data =>
            {
                if (data.Message.Content == "[]ping")
                {
                    await data.Message.RespondAsync("Pong! " + discord.Ping + " ms");
                }
                    
            };
#pragma warning disable 1998
            discord.GuildMemberAdded += async data =>
            {
                Console.WriteLine(data.Member.Nickname + " has joined a server");
            };
            discord.GuildMemberRemoved += async data =>
            {
                Console.WriteLine(" c ya nerd " + data.Member.Nickname);
            };
#pragma warning restore
            await discord.ConnectAsync();
            await Task.Delay(-1);
        }
    }
}
