using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PromptQuest.Web.Configuration;
using PromptQuest.Web.Models;
using PromptQuest.Web.Services;

namespace PromptQuest.Web.AI
{
    public class AiAgent : ICodeGenerationService
    {
        private readonly Client _client;
        private readonly IConfiguration _config;
        private readonly ILogger<AiAgent> _logger;

        private const string SystemPrompt =
            "Ты — ассистент в игровом проекте PromptQuest.\n" +
            "Это игра-головоломка: игрок пишет команды на естественном языке, " +
            "а ты превращаешь их в CSS для контейнера #pond.\n" +
            "Внутри #pond находятся лягушки (Frog) и лилии (Lily). " +
            "Управляя свойствами контейнера #pond, ты перемещаешь лягушек на сказанное место.\n\n" +
            "ПРАВИЛА (обязательны к соблюдению):\n" +
            "1. Меняй ТОЛЬКО контейнер #pond. Селекторы Frog и Lily не трогай. Исключение если есть grid.\n" +
            "3. Верни ИСКЛЮЧИТЕЛЬНО CSS-код ровно в таком формате:\n" +
            "   #pond { свойство: значение; свойство: значение; }\n" +
            "4. НЕ добавляй markdown-обёртки ``` или ```css, комментарии, пояснения, " +
            "приветствия, вопросы, текст до или после блока.\n" +
            "5. Ответ обязан начинаться с '#pond' и заканчиваться символом '}'.\n" +
            "6. Если команда игрока невыполнима — верни текущий CSS без изменений.\n\n" +
            "7. Каждый запрос перечитывай CSS присланного уровня. Не пытайся запомнить предыдущие данные"+
            "ТЕКУЩИЙ CSS ОБЪЕКТА #pond:\n" +
            "{CURRENT_CSS}\n\n" +
            "Примени команду игрока к текущему CSS и верни новый полный блок #pond { ... }.";

        public AiAgent(Client client, IConfiguration config, ILogger<AiAgent> logger)
        {
            _client = client;
            _config = config;
            _logger = logger;
        }

        public async Task<CodeGenerationResult> GenerateAsync(LevelDefinition level, string prompt, CancellationToken ct = default)
        {
            try
            {
                return new CodeGenerationResult(true, await AskAsync(prompt), false, CodeSource.Ai, null);
            }
            catch (InvalidOperationException)
            {
                return new CodeGenerationResult(true, "Не удалось получить ответ из ИИ. Возможно сервер сейчас перегружен.\n Попробуйте позже.", false, CodeSource.Ai, "Не удалось получить ответ из ИИ. Возможно сервер сейчас перегружен.\n Попробуйте позже.");
            }
            
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

