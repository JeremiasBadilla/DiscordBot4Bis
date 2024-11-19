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
                        ProcessMessageAttachments(message, prompt);

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
                        new { type = "text", text = $"pregunta" }
                    };

                    // Procesar los adjuntos del mensaje
                    ProcessMessageAttachments(message, prompt);
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
                if (attachment.ContentType.StartsWith("image/"))
                {
                    // Agregar imagen al prompt como URL
                    prompt.Add(new
                    {
                        type = "image_url",
                        image_url = new { url = attachment.Url }
                    });
                }
                else if (attachment.ContentType.StartsWith("text/"))
                {
                    // Descargar y agregar contenido del archivo de texto al prompt
                    using var client = new HttpClient();
                    var textContent = await client.GetStringAsync(attachment.Url);
                    prompt.Add(new
                    {
                        type = "text",
                        text = textContent
                    });
                }
                else if (attachment.ContentType.Equals("application/json", StringComparison.OrdinalIgnoreCase))
                {
                    // Descargar y procesar contenido del archivo JSON
                    using var client = new HttpClient();
                    var jsonContent = await client.GetStringAsync(attachment.Url);

                    try
                    {
                        var parsedJson = JsonConvert.DeserializeObject(jsonContent);

                        prompt.Add(new
                        {
                            type = "json",
                            json = parsedJson
                        });
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"[ERROR] Error al procesar JSON: {ex.Message}");
                    }
                }
                else if (attachment.ContentType.Equals("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", StringComparison.OrdinalIgnoreCase))
                {
                    // Descargar y procesar contenido de Excel
                    using var client = new HttpClient();
                    var excelStream = await client.GetStreamAsync(attachment.Url);

                    try
                    {
                        using var package = new OfficeOpenXml.ExcelPackage(excelStream);
                        var worksheet = package.Workbook.Worksheets[0];
                        var rows = new List<string>();

                        for (int row = 1; row <= worksheet.Dimension.Rows; row++)
                        {
                            var rowContent = string.Join(", ", Enumerable.Range(1, worksheet.Dimension.Columns).Select(col => worksheet.Cells[row, col].Text));
                            rows.Add(rowContent);
                        }

                        prompt.Add(new
                        {
                            type = "table",
                            content = string.Join("\n", rows)
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[ERROR] Error al procesar Excel: {ex.Message}");
                    }
                }
                //else if (attachment.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
                //{
                //    // Descargar y procesar contenido del PDF
                //    using var client = new HttpClient();
                //    var pdfStream = await client.GetStreamAsync(attachment.Url);

                //    try
                //    {
                //        using var pdfReader = new PdfReader(pdfStream);
                //        var pdfContent = new StringBuilder();

                //        for (int i = 1; i <= pdfReader.NumberOfPages; i++)
                //        {
                //            pdfContent.Append(PdfTextExtractor.GetTextFromPage(pdfReader, i));
                //        }

                //        prompt.Add(new
                //        {
                //            type = "pdf",
                //            text = pdfContent.ToString()
                //        });
                //    }
                //    catch (Exception ex)
                //    {
                //        Console.WriteLine($"[ERROR] Error al procesar PDF: {ex.Message}");
                //    }
                //}
                else
                {
                    // Ignorar adjuntos no compatibles
                    Console.WriteLine($"[INFO] Tipo de archivo no compatible: {attachment.ContentType}");
                }
            }

        }

        private async Task<string> SendPromptToOpenAIAsync(List<object> prompt)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);

                // Crear el cuerpo de la solicitud
                var requestBody = new
                {
                    model = "gpt-4o-mini",
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
