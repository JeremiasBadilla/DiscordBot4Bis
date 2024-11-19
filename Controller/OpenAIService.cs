using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Mail;

namespace DiscordBotAPI.Services
{
    public class OpenAIService
    {
        private const string ApiKey = "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";
        private const string ApiUrl = "https://api.openai.com/v1/chat/completions";

        public async Task AnswerQuestionAsync(SocketMessage message, string canalNombre, string pregunta)
        {
            try
            {
                if (message.Channel is SocketGuildChannel guildChannel)
                {
                    // Buscar el canal por nombre
                    var canal = guildChannel.Guild.TextChannels.FirstOrDefault(c => c.Name.Equals(canalNombre, StringComparison.OrdinalIgnoreCase));
                    if (canal != null)
                    {
                        // Obtener los mensajes recientes del canal
                        var channelMessages = new List<IMessage>();
                        await foreach (var batch in canal.GetMessagesAsync(100))
                        {
                            channelMessages.AddRange(batch);
                        }

                        string channelContent = string.Join("\n", channelMessages.Select(msg => $"{msg.Author.Username}: {msg.Content}"));

                        // Construir el prompt inicial con el contenido del canal
                        var prompt = new List<object>
                        {
                            new { type = "text", text = $"El usuario preguntó: '{pregunta}'. Aquí está el contexto del canal '{canalNombre}': {channelContent}" }
                        };

                        // Procesar los adjuntos del mensaje
                        await ProcessMessageAttachments(message, prompt);

                        // Enviar solicitud a OpenAI
                        var retorno = await SendPromptToOpenAIAsync(prompt);

                        await message.Channel.SendMessageAsync(retorno);
                    }
                    else
                    {
                        await message.Channel.SendMessageAsync("Canal no encontrado.");
                    }
                }
                else
                {
                    // Manejo de mensajes fuera de servidores
                    var prompt = new List<object>
                    {
                        new { type = "text", text = pregunta }
                    };

                    // Procesar los adjuntos del mensaje
                    await ProcessMessageAttachments(message, prompt);
                    var retorno = await SendPromptToOpenAIAsync(prompt);
                    // Enviar solicitud a OpenAI
                    await message.Channel.SendMessageAsync(retorno);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
                await message.Channel.SendMessageAsync("Hubo un error al procesar la solicitud.");
            }
        }

        private async Task ProcessMessageAttachments(SocketMessage message, List<object> prompt)
        {
            foreach (var attachment in message.Attachments)
            {
                try
                {
                    // Descargar archivo como Stream
                    using var client = new HttpClient();
                    var fileStream = await client.GetStreamAsync(attachment.Url);

                    // Procesar archivo basado en contenido detectado
                    var processedContent = await ProcessFileContent(fileStream, attachment.Filename, attachment.ContentType, attachment.Url);

                    if (processedContent != null)
                    {
                        prompt.Add(processedContent);
                    }
                    else
                    {
                        Console.WriteLine($"[INFO] Archivo no procesado: {attachment.Filename}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Error al procesar archivo: {attachment.Filename}, {ex.Message}");
                }
            }
        }

        private async Task<object?> ProcessFileContent(Stream fileStream, string filename, string contentType, string url)
        {
            try
            {
                // Categorización básica según contentType
                if (contentType.StartsWith("image/"))
                {
                    // Tratar como imagen
                    return new
                    {
                        type = "image_url",
                        image_url = new { url } // O directamente usar la URL si está disponible
                    };
                }
                else if (contentType.StartsWith("audio/"))
                {
                    // Tratar como audio
                    return new
                    {
                        type = "audio",
                        audio_url = filename // URL del archivo de audio
                    };
                }
                else
                {
                    // Todo lo demás: procesar como texto genérico desde el stream
                    using var reader = new StreamReader(fileStream);
                    var content = await reader.ReadToEndAsync();
                    return new
                    {
                        type = "text",
                        text = content
                    };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error procesando archivo {filename}: {ex.Message}");
                throw;
            }
        }


        // Procesar Excel (ya usado antes)
        private async Task<object?> ProcessExcelFile(Stream excelStream)
        {
            using var package = new OfficeOpenXml.ExcelPackage(excelStream);
            var worksheet = package.Workbook.Worksheets[0];
            var rows = new List<string>();

            for (int row = 1; row <= worksheet.Dimension.Rows; row++)
            {
                var rowContent = string.Join(", ", Enumerable.Range(1, worksheet.Dimension.Columns).Select(col => worksheet.Cells[row, col].Text));
                rows.Add(rowContent);
            }

            return new
            {
                type = "table",
                content = string.Join("\n", rows)
            };
        }




        private async Task<string> SendPromptToOpenAIAsync(List<object> prompt)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

                // Crear el cuerpo de la solicitud
                var requestBody = new
                {
                    model = "gpt-4o",
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 300
                };

                // Convertir la solicitud a JSON
                var jsonContent = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");

                // Enviar la solicitud a OpenAI
                var response = await client.PostAsync(ApiUrl, jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();

                    // Procesar la respuesta de la API
                    var jsonResponse = JObject.Parse(responseContent);
                    var respuesta = jsonResponse["choices"]?[0]?["message"]?["content"]?.ToString().Trim();

                    return respuesta ?? "No se pudo obtener una respuesta válida de OpenAI.";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Error en la solicitud a la API: {errorContent}");
                }
            }
        }
    }
}
