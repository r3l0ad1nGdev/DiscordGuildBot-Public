using DSharpPlus;
using System.Threading.Tasks;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using System.Linq;
using System.Text;

namespace GuildBot
{
    /// <summary>
    /// Extensions to DSharpPlus APIs.
    /// </summary>
    public static class DiscordApiExtensions
    {
        /// <summary>
        /// Creates the join message on our #join-guilds channel.
        /// </summary>
        /// <param name="discord"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        public static async Task<ulong> CreateJoinMessage(this DiscordClient discord, BotConfig config)
        {
            var channel = await discord.GetChannelAsync(BotEnvironment.JoinChannelId);
            var messages = await channel.GetMessagesAsync();
            if (messages != null && messages.Count > 0)
            {
                await channel.DeleteMessagesAsync(messages);
            }
            var description = new StringBuilder();
            foreach (var guild in config.Guilds)
            {
                description.AppendLine($"{guild.EmojiToJoin} = {guild.Name}");
            }
            var embed = new DiscordEmbedBuilder
            {
                Description = description.ToString(),
                Color = new DiscordColor(255, 255, 0),
                Title = Resources.EN_JOIN
            };

            var initmsg = await channel.SendMessageAsync(null, false, embed);

            foreach (var guild in config.Guilds)
            {
                await initmsg.CreateReactionAsync(DiscordEmoji.FromName(discord, guild.EmojiToJoin));
            }
            return initmsg.Id;
        }
        public static DiscordGuild ResolveGuild(this MessageReactionAddEventArgs data)
        {
            if (data.Message.Channel.Guild != null)
            {
                return data.Message.Channel.Guild;
            }
            return data.Client.Guilds.Values.First();
        }
        public static DiscordGuild ResolveGuild(this MessageCreateEventArgs data)
        {
            if (data.Message.Channel.Guild != null)
            {
                return data.Message.Channel.Guild;
            }
            return data.Client.Guilds.Values.First();
        }
    }
}
