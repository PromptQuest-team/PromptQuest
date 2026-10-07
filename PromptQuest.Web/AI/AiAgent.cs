using Google.GenAI;
using Google.GenAI.Types;
using Microsoft.Extensions.Options;
using PromptQuest.Web.Configuration;
using PromptQuest.Web.Models;
using PromptQuest.Web.Services;

namespace PromptQuest.Web.AI
{
    // Реализация ICodeGenerationService поверх Gemini. Не содержит знаний про
    // конкретную сцену (пруд/лягушка/кувшинка) — инструкция собирается из
    // level.SystemPrompt и level.AiScene конкретного уровня (разметка без
    // кувшинок и данных об их положении). level.Goal/level.Hint сюда никогда
    // не попадают — иначе модель решала бы уровень по описанию цели, минуя
    // промт игрока.
    public class AiAgent : ICodeGenerationService
    {
        // AskAsync — protected internal virtual и BuildSystemInstruction — internal
        // static исключительно для тестируемости (PromptQuest.Web.Tests подставляет
        // провайдер-заглушку через наследование / вызывает сборку инструкции прямо),
        // поведение обеих не меняется.

        private const string FormatRules =
            "Общие правила формата ответа (всегда соблюдаются, независимо от уровня):\n" +
            "- Верни ИСКЛЮЧИТЕЛЬНО CSS-код, без markdown-обёрток (``` или ```css), без комментариев, без пояснений, без приветствий.\n" +
            "- Ответ должен состоять только из CSS-правил и ничего больше.";

        private readonly Client _client;
        private readonly IConfiguration _config;
        private readonly IOptions<AppOptions> _options;
        private readonly ILogger<AiAgent> _logger;

        public AiAgent(Client client, IConfiguration config, IOptions<AppOptions> options, ILogger<AiAgent> logger)
        {
            _client = client;
            _config = config;
            _options = options;
            _logger = logger;
        }

        public async Task<CodeGenerationResult> GenerateAsync(LevelDefinition level, string prompt, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return new CodeGenerationResult(false, "", false, CodeSource.Ai, "Промт пуст.");
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(_options.Value.AiRequestTimeoutMs));

            try
            {
                var code = await AskAsync(level, prompt, timeoutCts.Token);

                if (string.IsNullOrWhiteSpace(code))
                {
                    _logger.LogWarning("Gemini returned an empty response for level {LevelId}.", level.Id);
                    return new CodeGenerationResult(false, "", false, CodeSource.Ai, "ИИ вернул пустой ответ.");
                }

                return new CodeGenerationResult(true, code, false, CodeSource.Ai, null);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Gemini request timed out after {TimeoutMs} ms for level {LevelId}.",
                    _options.Value.AiRequestTimeoutMs, level.Id);
                return new CodeGenerationResult(false, "", false, CodeSource.Ai, "Истекло время ожидания ответа от ИИ.");
            }
            catch (OperationCanceledException)
            {
                // Отмена вызвана внешним ct (например, клиент разорвал запрос) — не ошибка AI, просто пробрасываем.
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gemini request failed for level {LevelId}.", level.Id);
                return new CodeGenerationResult(false, "", false, CodeSource.Ai, "Не удалось получить ответ от ИИ. Попробуйте позже.");
            }
        }

        protected internal virtual async Task<string> AskAsync(LevelDefinition level, string prompt, CancellationToken ct)
        {
            var model = _config["Gemini:Model"];

            var config = new GenerateContentConfig
            {
                SystemInstruction = new Content
                {
                    Parts = new List<Part> { new Part { Text = BuildSystemInstruction(level) } }
                }
            };

            var response = await _client.Models.GenerateContentAsync(
                model: model,
                contents: prompt,
                config: config
            );

            var parts = response?.Candidates?
                .FirstOrDefault()?
                .Content?
                .Parts;

            if (parts is null || parts.Count == 0)
            {
                return string.Empty;
            }

            return string.Concat(parts.Select(p => p.Text));
        }

        // Инструкция собирается из systemPrompt конкретного уровня (с подстановкой
        // {CURRENT_CSS} из level.AiScene.BaseCss — не из полного scene.baseCss)
        // плюс общие правила формата и разметка level.AiScene.Html. goal/hint
        // уровня сюда не попадают ни в каком виде.
        internal static string BuildSystemInstruction(LevelDefinition level)
        {
            var instruction = (level.SystemPrompt ?? "").Replace("{CURRENT_CSS}", level.AiScene.BaseCss ?? "");

            return instruction
                + "\n\n" + FormatRules
                + "\n\nHTML сцены, которой ты управляешь:\n" + (level.AiScene.Html ?? "");
        }
    }
}
