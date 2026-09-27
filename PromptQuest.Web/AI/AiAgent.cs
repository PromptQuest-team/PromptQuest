using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PromptQuest.Web.Configuration;

namespace PromptQuest.Web.AI
{
    public class AiAgent
    {
        private readonly Client _client;
        private readonly IConfiguration _config;
        private readonly ILogger<AiAgent> _logger;

        private const string SystemPrompt =
            "Ты — ассистент в игровом проекте PromptQuest. " +
            "Отвечай кратко, по делу, на русском языке.";

        public AiAgent(Client client, IConfiguration config, ILogger<AiAgent> logger)
        {
            _client = client;
            _config = config;
            _logger = logger;
        }

        public async Task<string> AskAsync(string input, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var model = _config["Gemini:Model"];

            var config = new GenerateContentConfig
            {
                SystemInstruction = new Content
                {
                    Parts = new List<Part> { new Part { Text = SystemPrompt } }
                }
            };

            try
            {
                var response = await _client.Models.GenerateContentAsync(
                    model: model,
                    contents: input,
                    config: config
                );

                // Склеиваем ВСЕ части, а не только первую
                var parts = response?.Candidates?
                    .FirstOrDefault()?
                    .Content?
                    .Parts;

                if (parts is null || parts.Count == 0)
                    return string.Empty;

                return string.Concat(parts.Select(p => p.Text));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Gemini request failed. Model={Model}, InputLength={Len}",
                    model, input.Length);

                throw new InvalidOperationException(
                    "Не удалось получить ответ от AI. Попробуйте позже.", ex);
            }
        }
    }

}

