using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GuildBot
{

    public class BotReactionsStateMachine
    {
        CustomDiscordClient _discord;
        BotConfig _config;
        ulong _joinMessageId;

        public BotReactionsStateMachine(CustomDiscordClient discord, BotConfig config, ulong joinMessageId)
        {
            _discord = discord;
            _config = config;
            _joinMessageId = joinMessageId;
        }

        /// <summary>
        /// Entry point for all reactions to bot messages.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task ProcessMessageReaction(MessageReactionAddEventArgs data)
        {
            try
            {
                // This should not raise an exception. It's a bug in DSharpPlus
                if (data.User.IsBot)
                    return;
            }
            catch { }
            if (!data.Channel.IsPrivate && !Resources.ACCEPTED_CHANNELS.Any(id => id == data.Message.Channel.Id))
            {
                return;
            }
            // Reacted to the bot's initial message in the join channel
            if (data.Message.Id == _joinMessageId)
            {
                await ProcessInvitationToJoinMessage(data);
                return;
            }
            // check if its a answer to a question from a questionnaire
            if (data.Message.Embeds.Count > 0)
            {
                var questionParser = @"^""(?'guildTitle'[^\""]+)""\s+question\s+(?'questionNumber'\d+)\s+of\s+(?'numberOfQuestions'\d+)$";
                var match = Regex.Match(data.Message.Embeds[0].Title, questionParser);
                if (match.Success)
                {
#if DEBUG
                    Console.WriteLine($"Reacting to {data.Message.Id}, {data.Emoji.GetDiscordName()}, from {data.User.Username}");
#endif
                    var lockKey = $"{data.User.Username}|{data.Message.Id}";
                    if(IsLocked(lockKey))
                    {
#if DEBUG
                        Console.WriteLine($"{data.User.Username} is trigger happy!");
#endif
                        return;
                    }
                    LockMessage(lockKey);
#if DEBUG
                    Console.WriteLine($"Locking {lockKey}, {data.Emoji.GetDiscordName()}");
#endif
                    try
                    {
                        var guildName = match.Groups["guildTitle"].Value;
                        var questionNumber = int.Parse(match.Groups["questionNumber"].Value) - 1;
                        var numberOfQuestions = int.Parse(match.Groups["numberOfQuestions"].Value);
                        await ProcessAnswerToQuestion(data, guildName, questionNumber, numberOfQuestions);
                        return;
                    }
                    finally
                    {
//#if DEBUG
//                        Console.WriteLine($"Unlocking {lockKey}, {data.Emoji.GetDiscordName()}");
//#endif
//                        UnlockMessage(lockKey);
                    }
                }
            }
            // fall through normal processing
            switch (GetOriginalMessage(data.Message, _config))
            {
                case Resources.EN_JOIN_GUILD:
                    await StartQuestionnaire(data);
                    break;
                case Resources.EN_SUBMIT_YOUR_GUILD_APPLICTAION:
                    await ProcessJoinGuildApplicationSubmission(data);
                    break;
                default:
                    Console.Error.WriteLine($"{data.Message} is not supported. Sent by {data.Message.Author}");
                    break;
            }
        }

        void LockMessage(string id) => BotCacheService.Instance.Put(id, null);
        bool IsLocked(string id) => BotCacheService.Instance.ContainsKey(id);

        void UnlockMessage(string id) => BotCacheService.Instance.TryGet<object>(id, null, true);


        private async Task ProcessAnswerToQuestion(MessageReactionAddEventArgs data, string guildName, int questionNumber, int numberOfQuestions)
        {
            var emoji = data.Emoji.GetDiscordName();
            var score = BotCacheService.Instance.TryGet(data.Message.Id.ToString(), -1, true);
            var guild = _config.Guilds.FirstOrDefault(g => g.Name == guildName);
            if (score == -1)
            {
                var embed = new DiscordEmbedBuilder
                {
                    Description = "This application is no longer valid since you have not answerd the questions in a while. Please start again.",
                    Color = new DiscordColor(255, 0, 0),
                    Title = $"\"{guild.Name}\" application is no longer valid."
                };

                var sentMessage = await data.Message.RespondAsync("", false, embed);
                return;
            }
            if(guild == null)
            {
                Console.Error.WriteLine($"Missing guild {guildName}");
                return;
            }
            if (numberOfQuestions != guild.Questions.Count)
            {
                Console.Error.WriteLine($"Guild {guildName}, doesn't have that many questions ({numberOfQuestions})");
                return;
            }
            if (questionNumber > guild.Questions.Count - 1)
            {
                Console.Error.WriteLine($"Guild {guildName}, doesn't have that many questions ({questionNumber})");
                return;
            }
            var currentQuestion = guild.Questions[questionNumber];
            var reaction = currentQuestion.Reactions.FirstOrDefault(r => r.Name == emoji);
            if (reaction == null)
            {
                Console.Error.WriteLine($"Guild {guildName}, question {questionNumber} doesn't have emoji {emoji}");
                return;
            }
            var newScore = score + reaction.Score;
            if (questionNumber == guild.Questions.Count - 1)
            {
                //this was the last question in the questionaire
                await ProcessJoiningGuildQuestionScore(data, guild, newScore);
            }
            else
            {
                //post next question in list
                await CleanBotReactions(data.Message);
                var newQuestionMessage = await MakeReactions(_discord, data.Message, guild, questionNumber + 1);
                BotCacheService.Instance.Put(newQuestionMessage.Id.ToString(), newScore);
                return;
            }
        }
        /// <summary>
        /// Creates a DM when user reacts in "#join-guilds".
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private async Task ProcessInvitationToJoinMessage(MessageReactionAddEventArgs data)
        {
            var emoji = data.Emoji.GetDiscordName();
            foreach (var guild in _config.Guilds)
            {
                if (guild.EmojiToJoin != emoji)
                    continue;
                DiscordEmbedBuilder embed;
                var member = await GetMember(data);
                var isAllowed = IsMemberAllowedToApplyToJoinGuild(member, guild);
                if (isAllowed)
                {
                    embed = new DiscordEmbedBuilder
                    {
                        Description = @"Hello, you just applied to join a guild. We will ask you a few questions before accepting you."
                    + Environment.NewLine + "Are you ready to start?",
                        Color = new DiscordColor(255, 255, 0),
                        Title = $"{Resources.EN_JOIN_GUILD} \"{guild.Name}\""
                    };
                }
                else
                {
                    embed = new DiscordEmbedBuilder
                    {
                        Description = $@"Hello, you just applied to join a guild. Unfortunately, since you have already tried to apply to {guild.Name}"
                         + " recently and have been declined, you will have to wait to try again.",
                        Color = new DiscordColor(255, 255, 0),
                        Title = $"{Resources.EN_JOIN_GUILD} \"{guild.Name}\""
                    };
                    System.Threading.Thread.Sleep(100);
                    await data.Message.DeleteReactionAsync(data.Emoji, member);
                }
                
                await member.CreateDmChannelAsync();
                var sentMessage = await member.SendMessageAsync("", false, embed);
                if (isAllowed)
                {
                    await sentMessage.CreateReactionAsync(DiscordEmoji.FromName(_discord, ":white_check_mark:"));
                    // Avoid rate limiting errors on D-server
                    System.Threading.Thread.Sleep(100);
                    await sentMessage.CreateReactionAsync(DiscordEmoji.FromName(_discord, ":negative_squared_cross_mark:"));
                    System.Threading.Thread.Sleep(100);
                    await data.Message.DeleteReactionAsync(data.Emoji, member);

                }
                return;
            }
            Console.Error.WriteLine($"Unable to process reaction {emoji} for joining a guild.");

        }

        private bool IsMemberAllowedToApplyToJoinGuild(DiscordMember member, Guild guild)
        {
            var banId = $"{member.Id}-{guild.Name}";
            var result = BotCacheService.Instance.TryGet(banId, (KeepUntilCacheItem)null, false) == null;
            //if true, the user is allowed to proceed
            Console.WriteLine($"Checked {banId}, result: {result}");
            return result;
        }

        /// <summary>
        /// Checks whether a user wishes to proceed with the selected guild questionaire.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private async Task StartQuestionnaire(MessageReactionAddEventArgs data)
        {
            var emojiUserReactedTo = data.Emoji.GetDiscordName();
            var guildNameUserIndicatedWantsToJoin = GetGuildNameToJoin(data.Message.Embeds[0].Title);
            if (emojiUserReactedTo.Contains("white_check_mark") && data.Message.Author.IsBot)
            {
                foreach (var guild in _config.Guilds)
                {
                    if (guild.Name == guildNameUserIndicatedWantsToJoin)
                    {
                        //post first question in list
                        var newQuestionMessage = await MakeReactions(_discord, data.Message, guild, 0);
                        BotCacheService.Instance.Put(newQuestionMessage.Id.ToString(), 0);
                        await CleanBotReactions(data.Message);
                        return;
                    }
                }
                Console.Error.WriteLine("Invalid configuration: Received a reaction for a guild that does not exist in the current configuration.");   
            }
            else if (emojiUserReactedTo.Contains("negative_squared_cross_mark") && data.Message.Author.IsBot) // declined
            {
                try
                {
                    BotCacheService.Instance.TryGet(data.Message.Id.ToString(), (object)null, true);
                }
                catch{ }
                finally
                {
                    await data.Message.DeleteAsync();
                }
            }
        }
        /// <summary>
        /// Scoring guild questionnaire
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private async Task ProcessJoiningGuildQuestionScore(MessageReactionAddEventArgs data, Guild guildToJoin, int score)
        {
            var guild = data.ResolveGuild();
            var newembed = CreateJoinEmbed(guildToJoin.Name);
            var member = await guild.GetMemberAsync(data.User.Id);
            var sentMessage = await member.SendMessageAsync("", false, newembed);
            await sentMessage.CreateReactionAsync(DiscordEmoji.FromName(_discord, ":mailbox_with_mail:"));
            await sentMessage.CreateReactionAsync(DiscordEmoji.FromName(_discord, ":negative_squared_cross_mark:"));
            // TODO I did not find a way to keep the score in-band i.e. we cannot rely on message to keep the state.
            // see this link for context https://support.discord.com/hc/en-us/community/posts/360071912172-Add-Embed-Hidden-Fields
            // Issues are now bot crashing, state lost while application in flight
            // Other things may be related to multi-server scenarios where differnt bots may end up servicing same chat.
            BotCacheService.Instance.Put(sentMessage.Id.ToString(), score);

            await CleanBotReactions(data.Message);
        }
        
        /// <summary>
        /// Messages when confirmation or cancelation occurs.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private async Task ProcessJoinGuildApplicationSubmission(MessageReactionAddEventArgs data)
        {
            try
            {
                var guildName = data.Message.Embeds[0].Title.Replace(Resources.EN_SUBMIT_YOUR_GUILD_APPLICTAION, string.Empty).Trim();
                var score = (int)BotCacheService.Instance.Get(data.Message.Id.ToString(), true);
                var emojiUserReactedTo = data.Emoji.GetDiscordName();
                if (emojiUserReactedTo == ":mailbox_with_mail:")
                {
                    await SendApplicationToJoinWithScore(data, guildName, score);
                    await HandleScoreAndRoles(data, guildName, score);

                    var newembed = new DiscordEmbedBuilder
                    {
                        Title = "Thank you!",
                        Description = $"Your application {guildName} is being processed.",
                        Color = new DiscordColor(255, 255, 0),
                    };

                    var guild = data.ResolveGuild();
                    var member = await guild.GetMemberAsync(data.User.Id);
                    var sentMessage = await member.SendMessageAsync("", false, newembed);
                    var adjudicationResult = await AdjudicateApplicationToJoinGuild(guildName, score, data);
                    if (adjudicationResult == AdjudicationResult.None)
                    {
                        //TODO
                    }
                    else
                    {
                        if (adjudicationResult == AdjudicationResult.Declined)
                        {
                            var banId = $"{member.Id}-{guildName}";
                            Console.WriteLine($"Banning {banId}");
                            BotCacheService.Instance.Put(banId, new KeepUntilCacheItem(DateTime.Now.Add(new TimeSpan(0, 0, 30))));
                        }
                        await member.SendMessageAsync($"Your application has been {Enum.GetName(typeof(AdjudicationResult), adjudicationResult) }", false, null);
                    }
                }
                else
                {
                    var newembed = new DiscordEmbedBuilder
                    {
                        Title = "Thank you for your interest",
                        Description = $"Your application to join guild {guildName} is now cancelled.",
                        Color = new DiscordColor(255, 255, 0),
                    };

                    var guild = data.ResolveGuild();
                    var member = await guild.GetMemberAsync(data.User.Id);
                    var sentMessage = await member.SendMessageAsync("", false, newembed);
                }


                await CleanBotReactions(data.Message);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
            }

        }
        /// <summary>
        /// Message that contains the guild being applied to, applicant and score
        /// </summary>
        /// <param name="data"></param>
        /// <param name="guildName"></param>
        /// <param name="score"></param>
        /// <returns></returns>
        private async Task SendApplicationToJoinWithScore(MessageReactionAddEventArgs data, string guildName, int score)
        {
            var discordGuild = await _discord.GetGuildAsync(431403377977720862);
            var member = await discordGuild.GetMemberAsync(data.User.Id);
            var channel = discordGuild.GetChannel(771453181070671902);

            var message = $"A new application {guildName} was received from <@{data.User.Id}>" +
                 $"\nApplicant score: {score}. Application status: {await AdjudicateApplicationToJoinGuild(guildName, score, data)} ";

            await channel.SendMessageAsync(message);
        }

        private async Task <AdjudicationResult> AdjudicateApplicationToJoinGuild(string guildName, int score, MessageReactionAddEventArgs data)
        {
            var guild = _config.Guilds.FirstOrDefault(g => g.Name == guildName);
            var member = await GetMember(data);
            await member.CreateDmChannelAsync();

            if (guild == null)
            {
                Console.Error.WriteLine($"Guild {guild} doesn't exist");
                return AdjudicationResult.None;
            }

            if (score >= guild.MinScore)
            {
                return AdjudicationResult.Approved;
            }
            return AdjudicationResult.Declined;
        }

        /// <summary>
        /// assigns user a role depedning on their score
        /// </summary>
        /// <param name="data"></param>
        /// <param name="score"></param>
        /// <returns></returns>
        private async Task HandleScoreAndRoles(MessageReactionAddEventArgs data, string guildName, int score)
        {
            var discordGuild = await _discord.GetGuildAsync(431403377977720862);
            var member = await discordGuild.GetMemberAsync(data.User.Id);
            var guild = _config.Guilds.FirstOrDefault(g => g.Name == guildName);

            if (guild == null)
            {
                Console.Error.WriteLine($"Guild {guild} doesn't exist");
                return;
            }

            if (score >= guild.MinScore)
            {
                var role = discordGuild.Roles.Where<DiscordRole>(r => r.Name == guild.AssignedRole)?.FirstOrDefault();
                if (role != null)
                {
                    await discordGuild.GrantRoleAsync(member, role, null);
                }
                else
                {
                    Console.Error.WriteLine($"Role {guild.AssignedRole} for guild {guildName} doesn't exist");
                    return;
                }
                
            }
            else
            {
                //application is rejected
                //TODO add cooldown
            }
        }




        #region Utility methods
        /// <summary>
        /// cleans bot reactions to avoid reclicks
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private static async Task CleanBotReactions(DiscordMessage message)
        {
            foreach (var reaction in message.Reactions)
            {
                await message.DeleteOwnReactionAsync(reaction.Emoji);
            }
        }
        /// <summary>
        /// All these static methods are candidates for extension functions to be attached to corresponding type
        /// e.g. public static async Task<DiscordMember> GetMember(this MessageReactionAddEventArgs data)
        /// and then simply call await data.GetMember()
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private static async Task<DiscordMember> GetMember(MessageReactionAddEventArgs data)
        {
            DiscordMember member = null;
            if (data.Message.Channel.Guild == null)
            {
                member = await data.Client.Guilds.Values.First().GetMemberAsync(data.User.Id);
            }
            else try { member = await data.Message.Channel.Guild.GetMemberAsync(data.User.Id); } catch { }
            return member;
        }


        private static DiscordEmbedBuilder CreateJoinEmbed(string guildName)
        {
            return new DiscordEmbedBuilder
            {
                Title = $"{Resources.EN_SUBMIT_YOUR_GUILD_APPLICTAION}{guildName}",
                Description = $"React to this message to submit your application to join guild {guildName}.",
                Color = new DiscordColor(0, 255, 0)
            };
        }

        private static string GetOriginalMessage(DiscordMessage message, BotConfig config)
        {
            if (message.Embeds.Count > 0)
            {
                if (message.Embeds[0].Title.Contains(Resources.EN_JOIN_GUILD))
                {
                    return Resources.EN_JOIN_GUILD;
                }
                if (message.Embeds[0].Title.StartsWith(Resources.EN_SUBMIT_YOUR_GUILD_APPLICTAION))
                {
                    return Resources.EN_SUBMIT_YOUR_GUILD_APPLICTAION;
                }
            }
            
            return null;
        }

        private static string GetGuildNameToJoin(string title)
        {
            // TODO change this to exclude double quotes from the returned match to avoid trimming below
            var reg = new Regex("\".*?\""); 
            var matches = reg.Matches(title);
            foreach (var item in matches)
                return item.ToString().Trim('"');
            return null;
        }


        /// <summary>
        /// Builds the Embeded for the questions
        /// </summary>
        /// <param name="discord"></param>
        /// <param name="message"></param>
        /// <param name="guild"></param>
        /// <param name="questionIndex"></param>
        /// <returns></returns>
        private static async Task<DiscordMessage> MakeReactions(DiscordClient discord, DiscordMessage message, Guild guild, int questionIndex)
        {
            var embed = new DiscordEmbedBuilder
            {
                Description = guild.Questions[questionIndex].Prompt,
                Color = new DiscordColor(255, 255, 0),
                Title = $"\"{guild.Name}\" question {questionIndex + 1} of {guild.Questions.Count}"
            };

            var sentMessage = await message.RespondAsync("", false, embed);
            foreach (var reaction in guild.Questions[questionIndex].Reactions)
            {
                await sentMessage.CreateReactionAsync(DiscordEmoji.FromName(discord, reaction.Name));
                System.Threading.Thread.Sleep(100);
            }
            return sentMessage;
        }
        

        #endregion Utility methods
    }
}
