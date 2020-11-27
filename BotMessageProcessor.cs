using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace GuildBot
{
    public class BotMessageProcessor
    {
        private CustomDiscordClient discord;

        public BotMessageProcessor(CustomDiscordClient discord)
        {
            this.discord = discord;
        }

        internal async Task OnMessageCreated(MessageCreateEventArgs data)
        {
            //var discordGuild = await discord.GetGuildAsync(431403377977720862);
            var guild = data.ResolveGuild();
            var member = await guild.GetMemberAsync(data.Author.Id);
            var channel = data.Channel.Id;
            //if (!data.Message.Author.Id == )

            if (data.Message.Content.Contains("[]deleterole"))
            {
                var messageRole = data.Message.Content.Replace("[]deleterole", string.Empty).Trim();

                var role = guild.Roles.Where(r => r.Name == messageRole)?.FirstOrDefault();
                await member.RevokeRoleAsync(role);
            }


            //if (data.Message.Content == "[]TACTICALNUKE" && (member.PermissionsIn(data.Channel) & Permissions.Administrator) != 0)
            //{
            //    var channel = await discord.GetChannelAsync(data.Message.ChannelId);
            //    var messages = await channel.GetMessagesAsync();
            //    if (messages != null && messages.Count > 0)
            //    {
            //        await channel.DeleteMessagesAsync(messages);
            //    }
            //}

            if (data.Message.Content == "[]help")
            {
                await data.Message.RespondAsync("no, fuck off");
            }
            if (data.Message.Content == "[]fuckoff" && data.Message.Author.Username == "r3l0ad1nG")
            {
                //await data.Message.Channel.DeleteMessagesAsync();
                await data.Message.RespondAsync("no, fuck off");
            }
            if (data.Message.Content == "[]joke")
            {
                await data.Message.RespondAsync($"you're a joke {data.Author.Mention}");
            }
            if (data.Message.Content == "[]father")
            {
                await data.Message.RespondAsync($"r3l0ad3d#6118 is my dad");
            }

            if (data.Message.Content == "[]loadconfig")
            {                
                if ((member.PermissionsIn(data.Channel) & Permissions.Administrator) != 0)
                {
                    if (data.Message.Attachments.Count == 1)
                    {
                        var url = data.Message.Attachments[0].Url;
                        var jsonConfig = await GetAsync(url);
                        var botConfig = BotConfigBuilder.Build(jsonConfig);
                        var messageId = await discord.CreateJoinMessage(botConfig);
                        discord.AddMessageReactionAddedHandler(new BotReactionsStateMachine(discord, botConfig, messageId).ProcessMessageReaction);
                        Console.WriteLine("New configuration loaded...");
                    }
                }
                else
                {
                    await member.CreateDmChannelAsync();
                    var sentMessage = await member.SendMessageAsync("Hey, don't do that!", false, null);

                    Console.Error.WriteLine($"{data.Author.Username} tried to load a config in {guild.Name} on channel {data.Channel.Name}");
                }
                return;
            }
        }


        // TODO move this to a helper class
        internal static async Task<string> GetAsync(string uri)
        {
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream))
            {
                return await reader.ReadToEndAsync();
            }
        }
    }
}
