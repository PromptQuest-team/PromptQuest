# PromptQuest — архитектура и контракты системы

Описывает **текущую** реализацию проекта: обучающей веб-игры по
prompt-инжинирингу. Игрок видит сцену и цель, описывает нужный результат
словами, ИИ превращает промт в CSS, код применяется к сцене в изолированном
`iframe`, автоматический валидатор проверяет результат.

Дополнения, формулирующие принципы конструирования уровней и изоляции ИИ от
подсказок игрока — `SPEC-ADDENDUM-01.md` и `SPEC-ADDENDUM-02.md`. Планы на
будущее — раздел 15 этого документа.

---

## 1. Терминология

| Термин | Значение |
|---|---|
| **Уровень (Level)** | Единица задания: сцена, цель, набор проверок, системный промт |
| **Сцена (Scene)** | Фиксированные HTML + базовый CSS уровня |
| **Код игрока** | CSS, применяемый к сцене; в текущей реализации его пишет ИИ по промту игрока |
| **Попытка (Attempt)** | Один цикл «запустить и проверить». Считается по нажатию кнопки запуска |
| **Прогон (Run)** | Выполнение кода в sandbox-iframe и получение результата проверок |
| **Проверка (Check)** | Одно атомарное условие прохождения уровня |
| **Раннер (Runner)** | Документ внутри sandbox-iframe: собирает сцену, применяет код, выполняет проверки |
| **Игрок (Player)** | Анонимная сущность: GUID + никнейм, без пароля и регистрации |

---

## 2. Архитектура

### 2.1. Общая схема

```
Браузер                                    Сервер (ASP.NET Core)
┌──────────────────────────────┐            ┌────────────────────────────┐
│ UI (index.html + js-модули)  │  REST/JSON │ Endpoints (Minimal API)    │
│   ├─ ввод никнейма           │◄──────────►│   /api/players             │
│   ├─ выбор уровня            │            │   /api/levels              │
│   ├─ экран игры               │            │   /api/attempts            │
│   └─ таблица лидеров         │            │   /api/leaderboard         │
│            │                 │            │            │               │
│            │ postMessage     │            │            ▼               │
│            ▼                 │            │ Services                   │
│ ┌──────────────────────────┐ │            │   ILevelStore   (json)     │
│ │ sandbox <iframe>         │ │            │   IPlayerStore  (EF/mem)   │
│ │  runner.html + runner.js │ │            │   IAttemptStore (EF/mem)   │
│ │  сцена + код + проверки  │ │            │   ILeaderboardService      │
│ └──────────────────────────┘ │            │   ICodeGenerationService   │
└──────────────────────────────┘            │     (AiAgent → Gemini)     │
                                             └────────────────────────────┘
```

### 2.2. Стек

| Слой | Технология |
|---|---|
| Backend | ASP.NET Core 9, Minimal API, C#, один проект на API и статику |
| Frontend | HTML + CSS + чистый JavaScript (ES-модули), без фреймворков и сборщиков |
| Выполнение кода игрока/ИИ | `<iframe sandbox="allow-scripts">` **без** `allow-same-origin` |
| База данных | PostgreSQL + EF Core 9 (`Npgsql.EntityFrameworkCore.PostgreSQL`) |
| AI | Google Gemini через пакет `Google.GenAI` |
| Идентификация игрока | Никнейм + анонимный GUID в localStorage |
| Хостинг | не определён, Dockerfile/CI в репозитории нет |

### 2.3. Выбор реализации хранилищ

В `Program.cs`: если окружение `Development` **и** `PromptQuest:UseInMemoryStorage`
включён (так в `appsettings.Development.json`) — регистрируются in-memory
реализации (`Services/Storage/InMemory/`). Иначе обязательна валидная
`ConnectionStrings:Default` (проверяется на старте: непустые `Host`, `Database`,
`Username`) и регистрируются EF/Postgres-реализации (`Services/Storage/Db/`).

> **Известная проблема DI:** `Program.cs` регистрирует `ILeaderboardService`
> повторно, безусловно, как `InMemoryLeaderboardService`, уже после
> `if`/`else`-блока выбора хранилища. В .NET DI при нескольких регистрациях
> одного интерфейса побеждает последняя — то есть `EfLeaderboardService`
> фактически никогда не используется, а при запуске без in-memory режима
> попытка получить `ILeaderboardService` упадёт, т.к. `InMemoryPlayerStore`
> (от которого зависит `InMemoryLeaderboardService`) в этом режиме не
> зарегистрирован. Таблица лидеров работоспособна только в режиме
> `Development` + `UseInMemoryStorage=true`.

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
    Task<IReadOnlyList<LeaderboardEntryDto>> GetGlobalAsync(
        int take, CancellationToken ct = default);
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

**Текущие реализации:**

| Интерфейс | In-memory (`Storage/InMemory/`) | БД/ИИ (`Storage/Db/`, `AI/`) |
|---|---|---|
| `IPlayerStore` | `InMemoryPlayerStore` (`ConcurrentDictionary`) | `EfPlayerStore` (PostgreSQL) |
| `IAttemptStore` | `InMemoryAttemptStore` | `EfAttemptStore` |
| `ILeaderboardService` | `InMemoryLeaderboardService` | `EfLeaderboardService` (см. 2.3 — на практике не используется) |
| `ILevelStore` | `JsonLevelStore` — читает `Data/levels.json` при старте (используется всегда, независимо от режима хранения) |
| `ICodeGenerationService` | `AiAgent` (Gemini) — используется всегда; `ManualCodeGenerationService` существует в коде, но больше не регистрируется в DI |

> **Соответствие промта игрока и ответа модели реальному запросу к ИИ описано
> в `SPEC-ADDENDUM-01.md`. На практике текущая реализация `AiAgent` не
> полностью следует этому контракту** — не использует `level.SystemPrompt`
> (вместо него захардкожена одна системная инструкция под сцену «пруд,
> лягушка, кувшинка») и не передаёт `level.Scene.Html` вообще; `goal`/`hint`
> при этом действительно никогда не уходят в запрос — эта часть правила
> соблюдена.

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
    public int BestAttempts { get; set; }
    public int BestTimeMs { get; set; }
    public int BestScore { get; set; }
    public int TotalAttempts { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class Attempt
{
    public Guid Id { get; init; }
    public Guid PlayerId { get; init; }
    public string LevelId { get; init; } = "";
    public int AttemptNumber { get; init; }
    public string Prompt { get; init; } = "";
    public string Code { get; set; } = "";
    public CodeSource Source { get; init; }
    public bool? Passed { get; set; }
    public int ElapsedMs { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
}

public enum CodeSource { Manual = 0, Ai = 1 }
```

Схема БД (PostgreSQL, миграция `Migrations/20260929215005_InitialCreate`):
таблицы `Players`, `Attempts`, `LevelProgresses`; все первичные ключи —
GUID, генерируемые приложением (`ValueGeneratedNever`); `LevelProgresses`
имеет уникальный индекс `(PlayerId, LevelId)`; удаление игрока каскадно удаляет
его прогресс и попытки.

Модели уровня (`LevelDefinition`, `LevelScene`, `LevelValidation`, `LevelCheck`)
зеркально повторяют JSON-схему раздела 4. Десериализация — `System.Text.Json`
с `JsonNamingPolicy.CamelCase`; поле `kind` у проверки читается как **строка**
(не enum), чтобы новые виды проверок не требовали правок C#.

---

## 4. Формат описания уровня (JSON)

`Data/levels.json` — массив объектов уровня.

| Поле | Тип | Описание |
|---|---|---|
| `id` | string | Уникальный идентификатор, напр. `css-01-justify` |
| `order` | int | Порядок в списке, с 1 |
| `category` | string | сейчас всегда `css` |
| `title` | string | Название уровня для игрока |
| `goal` | string | Что должно получиться — видит только игрок, **никогда не уходит в запрос к ИИ** |
| `hint` | string | Подсказка по формулировке промта (не решение) |
| `difficulty` | int | 1–3 |
| `injectionMode` | string | сейчас всегда `styleAppend` |
| `scene.html` | string | HTML сцены, вставляется внутрь `#scene-root` |
| `scene.baseCss` | string | Базовый CSS, применяется до кода игрока |
| `codeTemplate` | string | Заготовка в поле кода |
| `systemPrompt` | string | Системный промт уровня; по контракту должен уходить в запрос к ИИ вместо промта игрока-подсказки (см. раздел 2.4 про фактическое поведение `AiAgent`) |
| `validation.tolerancePx` | int | Допуск для геометрических проверок (по умолчанию 8) |
| `validation.timeoutMs` | int | Предельное время прогона (по умолчанию 2000) |
| `validation.checks` | array | Массив проверок (раздел 6) |
| `forbiddenPatterns` | array | Регулярные выражения: совпадение → прогон провален с пояснением |

---

## 5. Каталог уровней

Все 6 уровней — задачи на геометрическое положение элементов внутри `#pond`
(flex/grid-контейнер), где находятся лягушка (`#frog`/`.frog`) и кувшинка
(`#lily`/`.lily`). `goal`/`hint` описывают результат в терминах сцены, не
называя CSS-свойство напрямую (принцип из `SPEC-ADDENDUM-01.md`).

| ID | Задача для игрока | Проверка |
|---|---|---|
| `css-01-justify` | Лягушка должна оказаться у правого края пруда | `overlapCenter(#frog,#lily)` |
| `css-02-align` | Лягушка должна опуститься к нижнему краю пруда | `overlapCenter(#frog,#lily)` |
| `css-03-center` | Лягушка должна оказаться ровно в центре пруда | `overlapCenter(#frog,#lily)` |
| `css-04-reverse` | Три лягушки — на кувшинках своего цвета, порядок зеркальный | 3 × `overlapCenter` |
| `css-05-spread` | Три лягушки равномерно по ширине, крайние прижаты к краям | 3 × `overlapCenter` |
| `css-06-grid` | Лягушка должна попасть в правую нижнюю клетку сетки 3×3 | `containedIn(#frog,#cell-9)` |

Планируется, что новые уровни также будут CSS-ориентированными; уровни на
CSS-селекторы и JavaScript (которые существовали в более ранней версии
каталога) из текущей реализации удалены — вместе с ними неактуальны любые
примеры в дополнениях, ссылающиеся на такие уровни (см. `SPEC-ADDENDUM-02.md`).

---

## 6. Движок валидации

Код, применяемый к сцене — недоверенный (его пишет ИИ по промту игрока). Он
выполняется в изолированном контексте и общается с приложением только
сообщениями.

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

## 8. Попытки, время, очки

### 8.1. Время

- Отсчёт начинается, когда экран игры отрисован и сцена показана игроку.
- Останавливается при получении первого успешного `RUN_RESULT`.
- Значение берётся из `performance.now()`, не `Date.now()`.
- При `visibilitychange → hidden` таймер ставится на паузу, при возврате возобновляется.
- Повторное открытие пройденного уровня начинает новую сессию: таймер и локальный
  счётчик попыток на экране обнуляются; в статистику идёт лучший результат.

### 8.2. Попытки

- Попытка засчитывается по нажатию кнопки запуска.
- Успешная попытка тоже считается: пройти с первого раза — 1 попытка, а не 0.
- Прогон с ошибкой выполнения или таймаутом считается попыткой.
- Пустой код (после trim строка пуста) попыткой не считается и не отправляется на сервер.

### 8.3. Очки

Формула реализуется на сервере в `ScoreCalculator`, чтобы клиент не мог
назначить себе счёт:

```
score = Max(0, 1000 - (attempts - 1) * 75 - (elapsedMs / 1000) * 2)
```

- `attempts` — порядковый номер попытки на сервере (накопительный счётчик для
  пары игрок+уровень, см. `IAttemptStore.CountAsync`), `elapsedMs` — время от
  открытия уровня до успеха, результат округляется вниз.
- Лучший результат по уровню — попытка с наибольшим `score`; при равенстве
  выигрывает меньшее время.
- Общий рейтинг — сумма лучших `score` по всем пройденным уровням.
- Новый результат перезаписывает личный рекорд только если он строго лучше.

---

## 9. HTTP API

Все эндпоинты возвращают JSON в camelCase. Ошибки — `ProblemDetails` с полем `detail`.

| Метод и путь | Назначение |
|---|---|
| `POST /api/players` | Создать игрока по никнейму, получить `playerId` |
| `GET /api/players/{playerId}` | Игрок и его прогресс; 404, если не найден |
| `GET /api/levels` | Список уровней (краткая форма, со статусом прохождения, если передан `playerId`) |
| `GET /api/levels/{levelId}` | Полное описание уровня: сцена, проверки, шаблон кода (без `systemPrompt` — он не уходит в браузер) |
| `POST /api/attempts` | Зарегистрировать попытку, получить код (сейчас — от ИИ) |
| `POST /api/attempts/{attemptId}/result` | Отправить итог прогона, получить очки |
| `GET /api/leaderboard/levels/{levelId}` | Топ по уровню (параметр `take`, по умолчанию 20) |
| `GET /api/leaderboard/global` | Общий топ по сумме очков |

`playerId` в ответах таблицы лидеров не отдаётся — только никнейм и цифры.

---

## 10. Фронтенд

Одна HTML-страница, четыре экрана (ввод никнейма, список уровней, игра,
таблица лидеров), переключаемых hash-роутингом (`#/levels`,
`#/play/<id>`, `#/leaderboard`).

На экране игры поле кода и кнопки управляются флагом `manualEntry`, который
приходит с сервера (`PromptQuest:ManualCodeEntry` в конфигурации) — фронтенд не
содержит жёстко зашитого режима:

| Поведение | `ManualCodeEntry = true` | `ManualCodeEntry = false` (текущее значение по умолчанию) |
|---|---|---|
| Поле промта | Основной элемент ввода, активно | То же |
| Поле кода | Редактируемое — человек вписывает код сам | Только для чтения, заполняется ответом ИИ |
| Кнопка «Отправить промт» | Скрыта/неактивна | Активна |
| Кнопка «Запустить код» | Активна | Скрыта — запуск автоматический после получения кода |
| Источник попытки | `source = "manual"` | `source = "ai"` |

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
    "LeaderboardTake": 20
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
- Расчёт очков выполняется на сервере: клиент присылает только факт
  прохождения, время и попытки — сервер им не обязан доверять в смысле
  повторной проверки геометрии (переверификации на сервере нет, см. раздел 14).
- `MaxPromptLength`/`MaxCodeLength` ограничивают длину промта и кода на сервере.
- Эндпоинты валидируют существование `playerId` и `levelId`; неизвестные
  идентификаторы дают 404.
- **Секреты (ключ Gemini API, строка подключения к БД) не должны храниться в
  `appsettings*.json`** — только через переменные окружения или
  `dotnet user-secrets`. На момент последнего аудита в `appsettings.json` был
  обнаружен закоммиченный ключ Gemini API — это требует немедленной ротации
  ключа и удаления секрета из текущего состояния репозитория и из истории git;
  исправление не входит в объём данного обновления документации.
- В `Program.cs` остаётся отладочный эндпоинт `GET /test-gemini`, не
  задействованный фронтендом: он без какой-либо авторизации обращается к
  реальному Gemini API. Это точка расхода платной квоты и кандидат на удаление.

---

## 13. Обработка ошибок и крайние случаи

| Ситуация | Поведение |
|---|---|
| Поле кода пустое (ручной режим) | Попытка не засчитывается, подсказка «введите код» |
| Синтаксическая ошибка CSS | Браузер игнорирует некорректные правила; проверки не проходят |
| Бесконечный цикл | Сторожевой таймер родителя удаляет iframe по `timeoutMs` |
| Код совпал с `forbiddenPatterns` | Прогон немедленно провален, без выполнения |
| ИИ не ответил / ошибка запроса | `AiAgent` перехватывает исключение, но текущая реализация при этом **не** выставляет `Success=false`: возвращает `Success=true` с текстом ошибки, записанным прямо в поле `Code` (которое затем применяется к сцене как CSS) — это отклонение от контракта `CodeGenerationResult` (раздел 2.4), а не осознанно спроектированное поведение |
| Сервер недоступен при отправке результата | Результат показывается локально; отправка не повторяется автоматически |
| localStorage очищен или содержит неизвестный серверу `playerId` | Игрок считается новым / сбрасывается на экран ввода никнейма |
| Перезапуск сервера в режиме in-memory | Данные теряются — прямое следствие отсутствия БД в этом режиме |

---

## 14. Известные ограничения и риски

- Нет автоматических тестов (`dotnet test` не применим — тестового проекта нет)
  и нет CI-конфигурации.
- Нет Dockerfile и конфигурации деплоя.
- Таблица лидеров нерабочая вне режима `Development` + in-memory (раздел 2.3).
- `AiAgent` игнорирует `level.SystemPrompt` и `level.Scene.Html`, использует
  захардкоженную системную инструкцию с неподставляемым плейсхолдером
  `{CURRENT_CSS}` в тексте — реальное содержимое текущего CSS в запрос не
  попадает (раздел 2.4).
- Переверификация результата на сервере отсутствует: клиент сообщает `passed`,
  `elapsedMs` и список проверок, сервер им доверяет при расчёте очков.
- Закоммиченный секрет Gemini API в `appsettings.json` (раздел 12) — приоритетный риск.
- `Microsoft.Extensions.AI` — зависимость в `.csproj`, не используемая кодом;
  `ManualCodeGenerationService` — реализация, больше не зарегистрированная в DI.

---

## 15. Планы на будущее

- Исправить регистрацию `ILeaderboardService` в DI (раздел 2.3), чтобы таблица
  лидеров работала в режиме с БД.
- Привести `AiAgent` к контракту `SPEC-ADDENDUM-01.md`: использовать
  `level.SystemPrompt` и `level.Scene.Html` вместо захардкоженной инструкции,
  убрать неподставляемый плейсхолдер `{CURRENT_CSS}`, исправить `Success`/`Error`
  при сбое запроса к ИИ.
- Убрать `/test-gemini` и неиспользуемые зависимости/классы (раздел 14).
- Ротация и вывод секретов из `appsettings.json` в переменные окружения /
  secret-менеджер; очистка истории git от закоммиченных значений.
- Ограничение частоты AI-запросов по `playerId`.
- Сохранение промтов вместе с результатом как исследовательский материал.
- Серверная переверификация прохождений.
- Редактор уровней для преподавателя (сейчас — только ручное редактирование
  `Data/levels.json`).
- Режим реального времени (несколько игроков на одном уровне).
- Аналитика промтов (средняя длина, типичные ошибки формулировок).
- Автоматические тесты и CI.
