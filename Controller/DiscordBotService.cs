using Discord.WebSocket;
using Discord;

namespace DiscordBotAPI.Services
{
    public class DiscordBotService
    {
        private readonly DiscordSocketClient _client;
        private readonly OpenAIService _openAIService;
        private static Dictionary<ulong, List<string>> conversationHistory = new();

        public DiscordBotService(OpenAIService openAIService)
        {
            _client = new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMessages
            });

            _openAIService = openAIService;

            _client.Log += LogAsync;
            _client.MessageReceived += MessageReceivedAsync;
            _client.Ready += ReadyAsync;
        }

        public async Task StartAsync()
        {
            const string DISCORD_TOKEN = "MTMwODI1MjQ5MzI0NzE1NjI2NQ.GxqcKG.vZ9TrNqfx4JmjQQl_0ctYJBQR3gLQxzbFbnz0E";
            await _client.LoginAsync(TokenType.Bot, DISCORD_TOKEN);
            await _client.StartAsync();
        }

        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }

        private Task ReadyAsync()
        {
            Console.WriteLine($"Bot conectado como {_client.CurrentUser}");
            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(SocketMessage message)
        {
            if (message.Author.IsBot)
                return;

            ulong channelId = message.Channel.Id;
            if (!conversationHistory.ContainsKey(channelId))
            {
                conversationHistory[channelId] = new List<string>();
            }

            if (message.Content.StartsWith("!"))
            {
                await ProcessCommandAsync(message);
            }
        }

        private async Task ProcessCommandAsync(SocketMessage message)
        {
            string[] args = message.Content.Substring(1).Split(' ', 2);
            string command = args[0].ToLower();

            switch (command)
            {
                case "canales":
                    await ListChannelsAsync(message);
                    break;
                case "preguntar":
                    if (args.Length > 1)
                    {
                        string[] preguntaArgs = args[1].Split(' ', 2);
                        string canalNombre = preguntaArgs[0];
                        string pregunta = preguntaArgs.Length > 1 ? preguntaArgs[1] : "";
                        await _openAIService.AnswerQuestionAsync(message, canalNombre, pregunta);
                    }
                    else
                    {
                        await message.Channel.SendMessageAsync("Especifica un canal y una pregunta.");
                    }
                    break;
                case "ayuda":
                    await SendHelpMessageAsync(message);
                    break;
                default:                   
                    await _openAIService.AnswerQuestionAsync(message, null, command);
                    break;
            }
        }

        private async Task ListChannelsAsync(SocketMessage message)
        {
            if (message.Channel is SocketGuildChannel guildChannel)
            {
                var canales = guildChannel.Guild.TextChannels;
                string canalesList = string.Join("\n", canales.Select(c => c.Name));
                await message.Channel.SendMessageAsync($"Canales disponibles:\n{canalesList}");
            }
            else
            {
                await message.Channel.SendMessageAsync("Este comando solo se puede usar en un servidor.");
            }
        }

        private async Task SendHelpMessageAsync(SocketMessage message)
        {
            string helpText = """
            **Comandos Disponibles:**
            - `!canales`: Lista los canales de texto disponibles (solo en un servidor).
            - `!preguntar [canal] [pregunta]`: Hace una pregunta sobre el contenido del canal especificado (solo en un servidor).
            - `!ayuda`: Muestra esta lista de comandos.
            """;
            await message.Channel.SendMessageAsync(helpText);
        }
    }
}
