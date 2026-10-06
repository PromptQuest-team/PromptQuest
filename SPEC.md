# PromptQuest — архитектура и контракты системы

Описывает **текущую** реализацию проекта: обучающей веб-игры по
prompt-инжинирингу. Игрок видит сцену и короткую цель, описывает нужный
результат словами, ИИ превращает промт в CSS, код применяется к сцене в
изолированном `iframe`, автоматический валидатор проверяет результат.
Единственная метрика успеха — длина промта.

Дополнения, формулирующие принципы конструирования уровней и изоляции ИИ от
подсказок игрока — `SPEC-ADDENDUM-01.md` и `SPEC-ADDENDUM-02.md`. Планы на
будущее — раздел 15 этого документа.

---

## 1. Терминология

| Термин | Значение |
|---|---|
| **Уровень (Level)** | Единица задания: сцена, вид сцены для ИИ, цель, набор проверок, системный промт |
| **Сцена (Scene)** | Фиксированные HTML + базовый CSS уровня — для рендера игроку и для валидатора |
| **Вид сцены для ИИ (AiScene)** | Урезанная версия сцены, уходящая в запрос к модели — без кувшинок и данных о положении цели |
| **Код игрока** | CSS, применяемый к сцене; пишет его ИИ по промту игрока |
| **Попытка (Attempt)** | Один цикл «отправить промт → получить код → проверить». Регистрируется на сервере при отправке промта |
| **Прогон (Run)** | Выполнение кода в sandbox-iframe и получение результата проверок |
| **Проверка (Check)** | Одно атомарное условие прохождения уровня |
| **Раннер (Runner)** | Документ внутри sandbox-iframe: собирает сцену, применяет код, выполняет проверки |
| **Игрок (Player)** | Анонимная сущность: GUID + никнейм, без пароля и регистрации |

---

## 2. Архитектура

### 2.1. Общая схема

```
Браузер                                    Сервер (ASP.NET Core)
┌──────────────────────────────┐            ┌─────────────────────────────┐
│ UI (index.html + js-модули)  │  REST/JSON │ Endpoints (Minimal API)     │
│   ├─ ввод никнейма           │◄──────────►│   /api/players              │
│   ├─ выбор уровня            │            │   /api/levels               │
│   ├─ экран игры               │            │   /api/attempts             │
│   └─ таблица лидеров         │            │   /api/leaderboard/levels   │
│            │                 │            │            │                │
│            │ postMessage     │            │            ▼                │
│            ▼                 │            │ Services                    │
│ ┌──────────────────────────┐ │            │   ILevelStore   (json)      │
│ │ sandbox <iframe>         │ │            │   IPlayerStore  (EF/mem)    │
│ │  runner.html + runner.js │ │            │   IAttemptStore (EF/mem)    │
│ │  сцена + код + проверки  │ │            │   ILeaderboardService       │
│ └──────────────────────────┘ │            │   AiRateLimiter (per player)│
└──────────────────────────────┘            │   ICodeGenerationService    │
                                             │     (AiAgent → Gemini)      │
                                             └─────────────────────────────┘
```

### 2.2. Стек

| Слой | Технология |
|---|---|
| Backend | ASP.NET Core 9, Minimal API, C#, один проект на API и статику |
| Frontend | HTML + CSS + чистый JavaScript (ES-модули), без фреймворков и сборщиков |
| Выполнение кода игрока/ИИ | `<iframe sandbox="allow-scripts">` **без** `allow-same-origin` |
| База данных | PostgreSQL + EF Core 9 (`Npgsql.EntityFrameworkCore.PostgreSQL`) |
| AI | Google Gemini через пакет `Google.GenAI` |
| Лимит запросов к ИИ | встроенный `System.Threading.RateLimiting`, партиция по `playerId`, без дополнительных пакетов |
| Идентификация игрока | Никнейм + анонимный GUID в localStorage |
| Хостинг | не определён, Dockerfile/CI в репозитории нет |

### 2.3. Выбор реализации хранилищ

`Program.cs` вычисляет `useInMemoryStorage` (окружение `Development` **и**
`PromptQuest:UseInMemoryStorage` включён — так в `appsettings.Development.json`)
и передаёт его вместе со строкой подключения в
`StorageRegistration.AddStorage(services, useInMemoryStorage, connectionString)`
(`Services/Storage/StorageRegistration.cs`). Метод регистрирует **ровно одну**
реализацию `IPlayerStore`/`IAttemptStore`/`ILeaderboardService` на вызов:
in-memory (`Services/Storage/InMemory/`) либо EF/Postgres
(`Services/Storage/Db/`, после проверки, что строка подключения задаёт
непустые `Host`, `Database`, `Username`). Выбор и обе ветки проверены тестом
(`PromptQuest.Web.Tests/ApiIntegrationTests.AddStorage_SelectsLeaderboardServiceByConfiguration`)
напрямую на `IServiceCollection`, без поднятия хоста целиком.

Логика вынесена из `Program.cs` в отдельный тестируемый метод умышленно:
top-level statements в `Program.cs` читают конфигурацию синхронно при старте,
до того как тестовый хост успел бы подставить свою — тестировать ветвление
через `WebApplicationFactory` для такого кода не получится.

### 2.4. Контракт хранилищ и генератора кода

Интерфейсы расположены в `Services/Storage/` (`IPlayerStore`, `IAttemptStore`,
`ILevelStore`) и `Services/` (`ILeaderboardService`, `ICodeGenerationService`).
Правила, которым они подчиняются:

| Правило | Почему важно |
|---|---|
| Все методы хранилищ и генератора кода асинхронные (`Task`/`Task<T>`) и принимают `CancellationToken` | Совместимость EF Core и сетевого вызова к ИИ |
| Прогресс игрока — `ICollection<LevelProgress>` с собственным `Id` и `PlayerId`, не `Dictionary` | Отображается в таблицу БД |
| Сохранение всегда явное: после изменения объекта вызывается метод хранилища | In-memory объект меняется сам, БД требует явной фиксации |
| `ICodeGenerationService` возвращает результат с признаком успеха и полем ошибки, не голую строку | Вызов ИИ может упасть, отвалиться по таймауту, упереться в лимиты |
| Все `Guid` генерирует приложение (`ValueGeneratedNever` в EF), а не БД | Иначе автоинкрементные ключи изменили бы порядок операций |
| Выборка и фильтрация — только внутри реализации хранилища, не в эндпоинтах | LINQ по словарю в памяти не превращается в SQL |

**Сигнатуры интерфейсов:**

```csharp
public interface IPlayerStore
{
    Task<Player?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Player>  CreateAsync(string nickname, CancellationToken ct = default);
    Task<LevelProgress?> GetProgressAsync(Guid playerId, string levelId,
                                          CancellationToken ct = default);
    Task UpsertProgressAsync(LevelProgress progress, CancellationToken ct = default);
}

public interface IAttemptStore
{
    Task<Attempt?> GetAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Attempt attempt, CancellationToken ct = default);
    Task UpdateAsync(Attempt attempt, CancellationToken ct = default);
    Task<int> CountAsync(Guid playerId, string levelId, CancellationToken ct = default);
}

public interface ILevelStore
{
    Task<IReadOnlyList<LevelDefinition>> GetAllAsync(CancellationToken ct = default);
    Task<LevelDefinition?> GetAsync(string levelId, CancellationToken ct = default);
}

public interface ILeaderboardService
{
    Task<IReadOnlyList<LeaderboardEntryDto>> GetForLevelAsync(
        string levelId, int take, CancellationToken ct = default);
}

public interface ICodeGenerationService
{
    Task<CodeGenerationResult> GenerateAsync(
        LevelDefinition level, string prompt, CancellationToken ct = default);
}

public sealed record CodeGenerationResult(
    bool       Success,
    string     Code,
    bool       ManualEntry,
    CodeSource Source,      // Manual | Ai
    string?    Error);
```

`ILeaderboardService` не имеет метода для общего зачёта — суммировать длины
промтов по разным уровням не имеет смысла как метрика (раздел 8).

**Текущие реализации:**

| Интерфейс | In-memory (`Storage/InMemory/`) | БД/ИИ (`Storage/Db/`, `AI/`) |
|---|---|---|
| `IPlayerStore` | `InMemoryPlayerStore` (`ConcurrentDictionary`) | `EfPlayerStore` (PostgreSQL) |
| `IAttemptStore` | `InMemoryAttemptStore` | `EfAttemptStore` |
| `ILeaderboardService` | `InMemoryLeaderboardService` | `EfLeaderboardService` |
| `ILevelStore` | `JsonLevelStore` — читает `Data/levels.json` при старте (используется всегда, независимо от режима хранения) |
| `ICodeGenerationService` | `AiAgent` (Gemini) — используется всегда; `ManualCodeGenerationService` существует в коде, но больше не регистрируется в DI |

**Как `AiAgent` строит запрос к ИИ (`AI/AiAgent.cs`):** системная инструкция
собирается из `level.SystemPrompt` (с подстановкой плейсхолдера
`{CURRENT_CSS}` значением `level.AiScene.BaseCss`), общих правил формата
ответа (только CSS, без markdown — захардкожены в коде, одинаковы для всех
уровней) и разметки `level.AiScene.Html`. Единственное сообщение пользователя
— промт игрока как есть. `level.Goal`/`level.Hint` нигде не читаются
(`SPEC-ADDENDUM-01.md`, раздел A). `level.Scene` (полная сцена с кувшинками)
в запрос не попадает — его заменяет `level.AiScene` (`SPEC-ADDENDUM-02.md`).

При любом сбое запроса (исключение, таймаут по `AppOptions.AiRequestTimeoutMs`,
пустой ответ) `GenerateAsync` возвращает `Success=false`, пустой `Code` и
причину в `Error` — текст ошибки никогда не попадает в `Code`.

---

## 3. Модель данных (C#)

```csharp
public sealed class Player
{
    public Guid Id { get; init; }
    public string Nickname { get; set; } = "";
    public DateTimeOffset CreatedAt { get; init; }
    public ICollection<LevelProgress> Progress { get; } = new List<LevelProgress>();
}

public sealed class LevelProgress
{
    public Guid Id { get; init; }
    public Guid PlayerId { get; init; }
    public string LevelId { get; init; } = "";
    public bool Completed { get; set; }
    public int BestPromptLength { get; set; } // единственная метрика рекорда
    public DateTimeOffset? CompletedAt { get; set; }

    // Исторические поля — больше не вычисляются новой логикой (раздел 8),
    // оставлены в схеме, чтобы не терять уже сохранённые данные.
    public int BestAttempts { get; set; }
    public int BestTimeMs { get; set; }
    public int BestScore { get; set; }
    public int TotalAttempts { get; set; }
}

public sealed class Attempt
{
    public Guid Id { get; init; }
    public Guid PlayerId { get; init; }
    public string LevelId { get; init; } = "";
    public int AttemptNumber { get; init; }
    public string Prompt { get; init; } = ""; // источник BestPromptLength
    public string Code { get; set; } = "";
    public CodeSource Source { get; init; }
    public bool? Passed { get; set; }
    public int ElapsedMs { get; set; } // больше не используется (раздел 8)
    public DateTimeOffset CreatedAt { get; init; }
}

public enum CodeSource { Manual = 0, Ai = 1 }
```

Схема БД (PostgreSQL, миграции `Migrations/20260929215005_InitialCreate` и
`Migrations/20261006122006_AddBestPromptLength`): таблицы `Players`,
`Attempts`, `LevelProgresses`; все первичные ключи — GUID, генерируемые
приложением (`ValueGeneratedNever`); `LevelProgresses` имеет уникальный индекс
`(PlayerId, LevelId)` и вспомогательный индекс `(LevelId, Completed,
BestPromptLength)` для лидерборда; удаление игрока каскадно удаляет его
прогресс и попытки. Миграции генерируются через
`Services/Storage/Db/AppDbContextFactory.cs` (`IDesignTimeDbContextFactory`) —
он нужен, поскольку `AppDbContext` регистрируется только в одной из двух
веток `StorageRegistration.AddStorage`, и обычная генерация миграций без него
не находит контекст.

Модели уровня (`LevelDefinition`, `LevelScene`, `LevelValidation`, `LevelCheck`)
зеркально повторяют JSON-схему раздела 4; `LevelDefinition.AiScene` — ещё один
экземпляр `LevelScene`, отдельный от `LevelDefinition.Scene`. Десериализация —
`System.Text.Json` с `JsonNamingPolicy.CamelCase`; поле `kind` у проверки
читается как **строка** (не enum), чтобы новые виды проверок не требовали
правок C#.

---

## 4. Формат описания уровня (JSON)

`Data/levels.json` — массив объектов уровня.

| Поле | Тип | Описание |
|---|---|---|
| `id` | string | Уникальный идентификатор, напр. `css-01-justify` |
| `order` | int | Порядок в списке, с 1 |
| `category` | string | сейчас всегда `css` |
| `title` | string | Название уровня для игрока |
| `goal` | string | Короткая цель без слов/цифр, выдающих положение — видит только игрок, **никогда не уходит в запрос к ИИ** |
| `hint` | string | Общая подсказка («Опиши ИИ то, что видишь на картинке») |
| `difficulty` | int | 1–3 |
| `injectionMode` | string | сейчас всегда `styleAppend` |
| `scene.html` | string | HTML полной сцены (с кувшинками), для рендера игроку и для валидатора |
| `scene.baseCss` | string | Базовый CSS полной сцены |
| `aiScene.html` | string | HTML, уходящий в запрос к ИИ — те же id управляемых элементов, без кувшинок |
| `aiScene.baseCss` | string | CSS, уходящий в запрос к ИИ (подставляется в `{CURRENT_CSS}`) — без правил кувшинок/положения цели |
| `codeTemplate` | string | Заготовка в поле кода (ручной режим) |
| `systemPrompt` | string | Системный промт уровня; уходит в запрос к ИИ вместе с `aiScene` |
| `validation.tolerancePx` | int | Допуск для геометрических проверок (по умолчанию 8) |
| `validation.timeoutMs` | int | Предельное время прогона (по умолчанию 2000) |
| `validation.checks` | array | Массив проверок (раздел 7) |
| `forbiddenPatterns` | array | Регулярные выражения: совпадение → прогон провален с пояснением |

---

## 5. Каталог уровней

Все 10 уровней — только про лягушек и кувшинки внутри `#pond` (flex- или
grid-контейнер). `goal`/`hint` не называют положение цели, направление,
число или CSS-свойство (`SPEC-ADDENDUM-01.md`); `aiScene` каждого уровня не
содержит кувшинок и данных о положении цели (`SPEC-ADDENDUM-02.md`) — оба
правила проверены тестами (`PromptQuest.Web.Tests/LevelCatalogTests.cs`).

| ID | Задача для игрока | Проверка |
|---|---|---|
| `css-01-justify` | Лягушка и кувшинка у правого края пруда | `overlapCenter(#frog,#lily)` |
| `css-02-align` | Лягушка и кувшинка у нижнего края пруда | `overlapCenter(#frog,#lily)` |
| `css-03-center` | Лягушка и кувшинка в центре пруда | `overlapCenter(#frog,#lily)` |
| `css-04-reverse` | Три лягушки на кувшинках своего цвета, зеркальный порядок | 3 × `overlapCenter` |
| `css-05-spread` | Три лягушки равномерно по ширине, крайние у краёв | 3 × `overlapCenter` |
| `css-06-grid` | Сетка 3×3, кувшинка в одной клетке | `containedIn(#frog,#cell-9)` |
| `css-07-two-spots` | Две лягушки, у каждой кувшинка в своей точке пруда | 2 × `overlapCenter` |
| `css-08-big-lily` | Кувшинка, занимающая 2×2 клетки сетки 3×3 | `containedIn(#frog,#lily-zone)` |
| `css-09-two-ponds` | Два независимых пруда-сетки (2×2 и 3×3), в каждом своя пара | 2 × `containedIn` |
| `css-10-four-colors` | Сетка 2×2, четыре лягушки на кувшинках своего цвета | 4 × `containedIn` |

Эталонные CSS-решения (для ручной проверки, не хранятся как отдельное поле):

| ID | Решение |
|---|---|
| `css-01-justify` | `#pond{justify-content:flex-end}` |
| `css-02-align` | `#pond{align-items:flex-end}` |
| `css-03-center` | `#pond{justify-content:center;align-items:center}` |
| `css-04-reverse` | `#pond{flex-direction:row-reverse}` |
| `css-05-spread` | `#pond{justify-content:space-between}` |
| `css-06-grid` | `#frog{grid-column:3;grid-row:3}` |
| `css-07-two-spots` | `#frog-a{left:372px;top:32px}` `#frog-b{left:52px;top:172px}` |
| `css-08-big-lily` | `#frog{grid-column:1 / span 2;grid-row:1 / span 2}` |
| `css-09-two-ponds` | `#frog-a{grid-column:2;grid-row:2}` `#frog-b{grid-column:2;grid-row:2}` |
| `css-10-four-colors` | `#frog-yellow{grid-column:1;grid-row:1}` `#frog-green{grid-column:2;grid-row:1}` `#frog-red{grid-column:1;grid-row:2}` `#frog-blue{grid-column:2;grid-row:2}` |

Планируется, что новые уровни также будут только про лягушек и кувшинки;
уровни на CSS-селекторы и JavaScript, существовавшие в более ранней версии
каталога, из текущей реализации удалены.

---

## 6. Движок валидации

Код, применяемый к сцене — недоверенный (его пишет ИИ по промту игрока). Он
выполняется в изолированном контексте и общается с приложением только
сообщениями. Раннер всегда получает полную `scene` (с кувшинками) — `aiScene`
используется только на сервере при построении запроса к ИИ и до браузера
игрока не доходит отдельно от обычного ответа `GET /api/levels/{id}` (который
`aiScene` и не включает, см. раздел 9).

### 6.1. Изоляция

- iframe создаётся с `sandbox="allow-scripts"` и **без** `allow-same-origin` —
  документ получает непрозрачный (opaque) источник.
- Код внутри не может обратиться к DOM родителя, к localStorage, к cookie и к
  API приложения.
- `event.origin` для сообщений из такого iframe равен строке `"null"`;
  подлинность проверяется сравнением `event.source` с `iframe.contentWindow` и
  совпадением `runId`.
- iframe пересоздаётся перед каждым прогоном. После получения результата он
  остаётся на экране (показывает игроку финальное состояние сцены) — удаляется
  досрочно только при срабатывании сторожевого таймера (бесконечный цикл).
- Сторожевой таймер на стороне родителя: если результат не пришёл за
  `validation.timeoutMs`, родитель удаляет iframe и засчитывает прогон как
  неуспешный с причиной `timeout`.

### 6.2. Протокол postMessage

`runId` — GUID, генерируемый родителем на каждый прогон.

```jsonc
// iframe → родитель: раннер загрузился и готов
{ "type": "RUNNER_READY" }

// родитель → iframe: задание на прогон
{
  "type": "RUN",
  "runId": "5f1c…",
  "injectionMode": "styleAppend",
  "scene": { "html": "…", "baseCss": "…" },
  "code": "#pond { justify-content: flex-end; }",
  "validation": { "tolerancePx": 8, "timeoutMs": 2000, "checks": [ ] },
  "forbiddenPatterns": []
}

// iframe → родитель: результат
{
  "type": "RUN_RESULT",
  "runId": "5f1c…",
  "passed": false,
  "checks": [
    { "id": "frog-on-lily", "passed": false,
      "description": "Лягушка находится на кувшинке",
      "expected": "центр (440, 44)", "actual": "центр (44, 44)" }
  ],
  "error": null
}
```

### 6.3. Режимы применения кода

В текущем каталоге уровней используется только `styleAppend` (код вставляется
как содержимое `<style id="player-code">` после базового CSS). Раннер
поддерживает также режимы `selector` (код — CSS-селектор, подставляется в
`querySelectorAll`) и `script` (код оборачивается в функцию и выполняется в
`try/catch`) — они реализованы в `wwwroot/sandbox/runner.js`, но сейчас не
используются ни одним уровнем каталога.

### 6.4. Порядок прогона внутри раннера

1. Очистить `#scene-root` и удалить `<style id="player-code">` от предыдущего прогона.
2. Вставить `scene.baseCss` в `<style id="base-css">`, затем `scene.html` в `#scene-root`.
3. Проверить `forbiddenPatterns` — совпадение проваливает прогон немедленно, без выполнения кода.
4. Применить код игрока согласно `injectionMode`.
5. Дождаться завершения раскладки: два подряд `requestAnimationFrame`.
6. Выполнить проверки из `validation.checks` по порядку, собрать массив результатов.
7. `passed` = все проверки пройдены и `error` отсутствует.
8. Отправить `RUN_RESULT` родителю.

### 6.5. Требования к конструированию сцены

Обязательны для `scene`/`aiScene` любого уровня — нарушение считается багом
сцены, а не валидатора:

1. **Лягушки и декоративные клетки не должны зависеть от авто-размещения
   CSS Grid, если кувшинка размещена явно.** CSS Grid сначала резервирует
   место под элементы с явным `grid-column`/`grid-row`, и только потом
   раскладывает остальные («авто») элементы по оставшимся местам. Если
   декоративная клетка (`.cell`) размещена авто, а кувшинка, занимающая ту же
   клетку — явно, авто-алгоритм считает эту клетку занятой и сдвигает саму
   декоративную клетку (и всё, что идёт за ней в DOM) в следующую свободную
   —  сетка 3×3 превращается в «лесенку» из неполных рядов, а проверка
   сравнивает лягушку не с той клеткой, которая видна на экране. Поэтому во
   всех grid-уровнях у **каждой** клетки и у лягушки — явный
   `grid-column`/`grid-row`; ничего не остаётся на авто-размещение.
2. Кувшинка — `position:absolute` внутри позиционированного контейнера
   (flex-уровни) либо `position:static` с явным `grid-column`/`grid-row`,
   перекрывающим клетку-цель (grid-уровни) — в обоих случаях она не участвует
   в раскладке соседей.
3. Сцена целиком помещается в `.sandbox-frame` (≈480px по ширине, 330px по
   высоте, `wwwroot/css/app.css`) без скролла. `wwwroot/sandbox/runner.html`
   ставит `overflow:hidden` и нулевые отступы на `html`/`body` как последний
   рубеж — сцену всё равно нужно размерить так, чтобы в этом не было нужды.
4. Кувшинка (`.lily`) — круг или овал со сплошной непрозрачной заливкой
   контрастного цвета; общий стиль в `runner.html` даёт форму и запасной
   цвет, но уровень, задающий свой цвет, должен делать это явно для каждого
   `.lily`-элемента на сцене.
5. В исходном положении (до решения игрока) лягушка и кувшинка не
   перекрываются.

Пункты 3–5 проверены тестом
`PromptQuest.Web.Tests/RunnerBrowserTests.Scene_DoesNotOverflow_AndLilyIsVisible`
в настоящем Chromium (раздел 14); пункты 1–2 проверяются тем же тестовым
классом косвенно — через `ReferenceSolution_Passes_EmptyCode_Fails`, который
не прошёл бы, если бы клетка/лягушка оказались не там, где их ожидает проверка.

---

## 7. Виды проверок

Реализованы в `wwwroot/sandbox/runner.js` как словарь функций
`(check, ctx) => CheckResult`. Неизвестный `kind` — ошибка прогона, а не тихий
пропуск.

| kind | Поля | Условие прохождения | Используется каталогом сейчас |
|---|---|---|---|
| `overlapCenter` | `subject`, `target` | Центры совпадают в пределах `tolerancePx` по обеим осям | да |
| `containedIn` | `subject`, `target` | `subject` полностью внутри `target` с допуском `tolerancePx` | да |
| `orderX` / `orderY` | `selectors` | Элементы расположены в заданном порядке по X/Y | нет |
| `computedStyle` | `subject`, `property`, `expected` | `getComputedStyle` равно `expected` (строгое сравнение после trim) | нет |
| `selectorMatches` | `expectedIds` | Множество `id`, выбранных селектором игрока, совпадает с `expectedIds` | нет |
| `textContent` | `subject`, `expected` | `textContent` после trim равно `expected` | нет |
| `classOnElements` | `selector`, `className`, `expectedIds` | Класс присутствует ровно у элементов с нужными `id` | нет |
| `elementCount` | `selector`, `expected` | Количество элементов под `selector` равно `expected` | нет |

Каждая функция возвращает `id`, `passed`, `description`, `expected`, `actual` —
последние два поля человекочитаемые, показываются игроку как объяснение.

---

## 8. Метрика: длина промта

- Единственный показатель успеха на уровне — длина промта в Unicode-символах
  **после `Trim()`**. Считается на сервере, по тексту промта, уже сохранённому
  в `Attempt.Prompt` — значение от клиента не принимается.
  Учитываются только успешные попытки (`Passed = true`).
- Личный рекорд по уровню — `LevelProgress.BestPromptLength`, минимальная
  длина среди успешных попыток игрока; новый результат заменяет рекорд только
  если он строго короче.
- Время прохождения и число попыток **не участвуют** ни в рекордах, ни в
  рейтинге, ни в начислении чего-либо — поля `BestAttempts`/`BestTimeMs`/
  `BestScore`/`TotalAttempts`/`Attempt.ElapsedMs` остаются в схеме (раздел 3),
  но новой логикой не вычисляются. `ScoreCalculator` удалён из кода.
- Попытка регистрируется на сервере (`POST /api/attempts`) при отправке
  промта; при сбое ИИ (раздел 2.4) или срабатывании лимита запросов (раздел 9)
  попытка не создаётся.
- Пустой промт (после `Trim()`) не отправляется в `GenerateAsync` — `AiAgent`
  возвращает `Success=false` без обращения к Gemini.

---

## 9. HTTP API

Все эндпоинты возвращают JSON в camelCase. Ошибки — `ProblemDetails` с полем `detail`.

| Метод и путь | Назначение |
|---|---|
| `POST /api/players` | Создать игрока по никнейму, получить `playerId` |
| `GET /api/players/{playerId}` | Игрок и его прогресс (`promptLength` по каждому уровню); 404, если не найден |
| `GET /api/levels` | Список уровней (краткая форма, с `bestPromptLength`, если передан `playerId`) |
| `GET /api/levels/{levelId}` | Полное описание уровня: `scene`, проверки, шаблон кода (без `systemPrompt` и без `aiScene` — они не уходят в браузер) |
| `POST /api/attempts` | Зарегистрировать попытку и получить код от ИИ. `429` с заголовком `Retry-After` при превышении лимита запросов на игрока (по умолчанию 10/мин, `AppOptions.AiRequestsPerMinutePerPlayer`); `502`, если ИИ не ответил — в обоих случаях попытка не создаётся |
| `POST /api/attempts/{attemptId}/result` | Отправить итог прогона, получить `promptLength` и факт личного рекорда |
| `GET /api/leaderboard/levels/{levelId}` | Топ по уровню (параметр `take`, по умолчанию 20), сортировка по возрастанию `promptLength`, при равенстве — раньше завершивший выше |

Общего зачёта нет (раздел 2.4). `playerId` в ответах таблицы лидеров не
отдаётся — только никнейм и `promptLength`.

---

## 10. Фронтенд

Одна HTML-страница, четыре экрана (ввод никнейма, список уровней, игра,
таблица лидеров по уровню), переключаемых hash-роутингом (`#/levels`,
`#/play/<id>`, `#/leaderboard`).

На экране игры под полем промта — счётчик символов (считает `Trim()`-длину в
реальном времени, тем же способом, что и сервер при подсчёте рекорда). Поле
кода и кнопки управляются флагом `manualEntry`, который приходит с сервера
(`PromptQuest:ManualCodeEntry` в конфигурации) — фронтенд не содержит жёстко
зашитого режима:

| Поведение | `ManualCodeEntry = true` | `ManualCodeEntry = false` (текущее значение по умолчанию) |
|---|---|---|
| Поле промта | Основной элемент ввода, активно | То же |
| Поле кода | Редактируемое — человек вписывает код сам | Только для чтения, заполняется ответом ИИ |
| Кнопка «Отправить промт» | Скрыта/неактивна | Активна |
| Кнопка «Запустить код» | Активна | Скрыта — запуск автоматический после получения кода |
| Источник попытки | `source = "manual"` | `source = "ai"` |

При ошибке от сервера экран игры различает: `429` → «Слишком часто, подождите
N секунд» (N — из `Retry-After`), `502` → «ИИ временно недоступен, попробуйте
ещё раз» — в обоих случаях валидатор не запускается (сервер ничего не создал).

При старте приложение проверяет сохранённый в `localStorage` `playerId` на
сервере (`GET /api/players/{id}`) и сбрасывает его локально, если сервер его
не знает (например, после потери данных in-memory хранилища) — без этого
игрок застревал бы на ошибке «игрок не найден».

---

## 11. Конфигурация

```jsonc
// appsettings.json
{
  "PromptQuest": {
    "ManualCodeEntry": false,
    "LevelsFilePath": "Data/levels.json",
    "MaxPromptLength": 2000,
    "MaxCodeLength": 8000,
    "DefaultTolerancePx": 8,
    "DefaultTimeoutMs": 2000,
    "LeaderboardTake": 20,
    "AiRequestTimeoutMs": 10000,
    "AiRequestsPerMinutePerPlayer": 10
  },
  "Gemini": {
    "Model": "<имя модели>"
    // "ApiKey" задаётся через переменную окружения Gemini__ApiKey или
    // dotnet user-secrets, не хранится в файле — см. раздел 12.
  }
}

// appsettings.Development.json
{
  "PromptQuest": { "UseInMemoryStorage": true }
}
```

`ConnectionStrings:Default` не хранится в файлах конфигурации — только через
переменную окружения `ConnectionStrings__Default` или `dotnet user-secrets`;
обязательна во всех режимах, кроме `Development` с `UseInMemoryStorage=true`.

`Configuration/AppOptions.cs` содержит также свойство `GeminiModel`, которое
сейчас не используется кодом генерации (`AiAgent` читает модель из `Gemini:Model`
через `IConfiguration` напрямую, а не через `AppOptions`) — это несогласованность
в текущей реализации, а не два равнозначных способа настройки.

---

## 12. Безопасность и секреты

- Код, применяемый к сцене, никогда не выполняется на сервере — только в
  sandbox-iframe в браузере игрока, без `allow-same-origin`.
- Длина промта для рекорда считается на сервере из уже сохранённого
  `Attempt.Prompt` — клиент не может повлиять на значение (раздел 8). Геометрию
  сцены при этом проверяет только браузер игрока: сервер доверяет присланному
  `passed` — переверификации на сервере нет (раздел 13).
- `MaxPromptLength`/`MaxCodeLength` ограничивают длину промта и кода на сервере.
- `AiRateLimiter` ограничивает число запросов к ИИ на `playerId` в минуту
  (раздел 9) — защита от неконтролируемого расхода платной квоты Gemini.
- Эндпоинты валидируют существование `playerId` и `levelId`; неизвестные
  идентификаторы дают 404.
- **Секреты (ключ Gemini API, строка подключения к БД) не должны храниться в
  `appsettings*.json`** — только через переменные окружения или
  `dotnet user-secrets`. На момент последнего аудита в `appsettings.json` был
  обнаружен закоммиченный ключ Gemini API — это требует немедленной ротации
  ключа и удаления секрета из текущего состояния репозитория и из истории git.

---

## 13. Обработка ошибок и крайние случаи

| Ситуация | Поведение |
|---|---|
| Поле кода пустое (ручной режим) | Попытка не засчитывается, подсказка «введите код» |
| Промт пуст (после `Trim()`) | `AiAgent` возвращает `Success=false` без обращения к Gemini; попытка не создаётся |
| Синтаксическая ошибка CSS | Браузер игнорирует некорректные правила; проверки не проходят |
| Бесконечный цикл | Сторожевой таймер родителя удаляет iframe по `timeoutMs` |
| Код совпал с `forbiddenPatterns` | Прогон немедленно провален, без выполнения |
| ИИ не ответил / исключение / таймаут / пустой ответ | `GenerateAsync` возвращает `Success=false`, пустой `Code`, причину в `Error`; эндпоинт отвечает `502`, попытка не создаётся |
| Превышен лимит запросов к ИИ на игрока | `429` с заголовком `Retry-After`; попытка не создаётся |
| Сервер недоступен при отправке результата | Результат показывается локально; отправка не повторяется автоматически |
| localStorage очищен или содержит неизвестный серверу `playerId` | Игрок считается новым / сбрасывается на экран ввода никнейма |
| Перезапуск сервера в режиме in-memory | Данные теряются — прямое следствие отсутствия БД в этом режиме |

---

## 14. Известные ограничения и риски

- Нет CI-конфигурации и Dockerfile — тесты (раздел «Тесты» в `README.md`)
  запускаются только вручную; браузерные тесты (`RunnerBrowserTests`) к тому же
  требуют разового `playwright.ps1 install chromium` на машине до первого запуска.
- Переверификация результата на сервере отсутствует: клиент сообщает
  `passed` и список проверок, сервер им доверяет при обновлении рекорда.
- Закоммиченный секрет Gemini API в `appsettings.json` (раздел 12) — приоритетный риск.
- `Microsoft.Extensions.AI` — зависимость в `.csproj`, не используемая кодом;
  `ManualCodeGenerationService` — реализация, больше не зарегистрированная в DI;
  исторические поля `LevelProgress.BestAttempts/BestTimeMs/BestScore/TotalAttempts`
  и `Attempt.ElapsedMs` остаются в схеме, но не вычисляются (раздел 8).

---

## 15. Планы на будущее

- Ротация и вывод секретов из `appsettings.json` в переменные окружения /
  secret-менеджер; очистка истории git от закоммиченных значений.
- Серверная переверификация прохождений.
- Редактор уровней для преподавателя (сейчас — только ручное редактирование
  `Data/levels.json`).
- Режим реального времени (несколько игроков на одном уровне).
- Аналитика промтов (средняя длина успешного промта, типичные ошибки
  формулировок) — `Attempt.Prompt` уже сохраняется, аналитики над этими
  данными пока нет.
- CI-конфигурация, запускающая `dotnet build`/`dotnet test` автоматически.
- Удаление неиспользуемых зависимостей/классов (раздел 14).
