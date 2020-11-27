using System.Collections.Generic;
using System.Text.Json;

namespace GuildBot
{
    public enum AdjudicationResult { None, Approved, Declined}
    public class BotConfig
    {
        public GuildPrompt JoinGuildPrompt { get; set; }
        public IList<Guild> Guilds { get; set; }
    }
    public static class BotConfigBuilder
    {
        public static BotConfig Build(string jsonString)
        {
            return JsonSerializer.Deserialize<BotConfig>(jsonString);
        }
    }

    public class Guild
    {
        public string Name { get; set; }
        public string AssignedRole { get; set; }
        public string EmojiToJoin { get; set; }
        public IList<Question> Questions { get; set; }
        public int MinScore { get; set; }


    }
    public class Question
    {
        public string Prompt { get; set; }
        public IList<Reaction> Reactions { get; set; }
    }

    public class Reaction
    {
        public string Name { get; set; }
        public int Score { get; set; }
    }

    public class GuildPrompt
    {
        public string Description { get; set; }
    }


}
