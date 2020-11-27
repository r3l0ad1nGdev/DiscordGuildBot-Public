using DSharpPlus;
using DSharpPlus.EventArgs;

namespace GuildBot
{
    /// <summary>
    /// This class allows us to keep track of event handlers so that they can be removed when a new configuration is loaded
    /// </summary>
    public class CustomDiscordClient : DiscordClient
    {
        AsyncEventHandler<MessageReactionAddEventArgs> _handler;
        public void AddMessageReactionAddedHandler(AsyncEventHandler<MessageReactionAddEventArgs> handler)
        {
            if (_handler != null)
            {
                this.MessageReactionAdded -= _handler;
            }
            this.MessageReactionAdded += handler;
            _handler = handler;
        }
        public CustomDiscordClient(DiscordConfiguration config) : base(config)
        {
        }
    }
}
