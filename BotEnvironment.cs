using System;

namespace GuildBot
{
    internal class BotEnvironment
    {
        //Channel where message to join guilds appears, MUST BE CHANGED
        internal ulong JoinChannelId = 0;        
    }

    internal class Resources
    {
        internal const string EN_JOIN = "React to this message to join one of our guilds.";
        internal const string EN_JOIN_GUILD = "Joining guild";

        internal const string EN_SUBMIT_YOUR_GUILD_APPLICTAION = "Submit your application to join guild ";

        //channels that the bot will proccess reactions in
        internal static readonly ulong[] ACCEPTED_CHANNELS = new ulong[] { 771453424361013248, 544077403949891605 };
    }

}
