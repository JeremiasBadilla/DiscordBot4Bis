using Discord;
using Discord.WebSocket;
using OpenAI.Chat;
using System.Collections.Generic;

namespace DiscordBotAPI.Services
{
    public class OpenAIService
    {
        private readonly ChatClient _client;

        public OpenAIService()
        {
            const string OPENAI_API_KEY = "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";
            _client = new ChatClient(model: "gpt-4o", apiKey: OPENAI_API_KEY);
        }

        public async Task AnswerQuestionAsync(SocketMessage message, string canalNombre, string pregunta)
        {
            try
            {


                if (message.Channel is SocketGuildChannel guildChannel)
                {
                    var canal = guildChannel.Guild.TextChannels.FirstOrDefault(c => c.Name.Equals(canalNombre, StringComparison.OrdinalIgnoreCase));
                    if (canal != null)
                    {
                        var channelMessages = new List<IMessage>();
                        await foreach (var batch in canal.GetMessagesAsync(100))
                        {
                            channelMessages.AddRange(batch);
                        }

                        string channelContent = string.Join("\n", channelMessages.Select(msg => $"{msg.Author.Username}: {msg.Content}"));

                        try
                        {
                            // Solicitud básica a OpenAI
                            //TODO ESTA MAL
                            string prompt = $"El usuario preguntó: '{pregunta}'. Aquí está el contexto del canal '{canalNombre}': {channelContent}";
                            ChatCompletion completion = await _client.CompleteChatAsync(prompt);

                            // Respuesta de OpenAI
                            string response = completion.Content[0].Text;
                            await message.Channel.SendMessageAsync(response);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[ERROR] Error al procesar la solicitud: {ex}");
                            await message.Channel.SendMessageAsync("Hubo un error al procesar la solicitud.");
                        }
                    }
                    else
                    {
                        await message.Channel.SendMessageAsync("Canal no encontrado.");
                    }
                }
                else
                {
                    ChatCompletion completion = await _client.CompleteChatAsync(pregunta);
                    string response = completion.Content[0].Text;
                    await message.Channel.SendMessageAsync(response);
                    //await message.Channel.SendMessageAsync("Este comando solo se puede usar en un servidor.");
                }
            }
            catch (Exception ex) 
            {
                string exceptionMessage = $"[ERROR] Error al responder la pregunta: {ex}";
                throw;
            }
        }
    }
}
