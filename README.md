# PromptQuest

Обучающая веб-игра для тренировки prompt-инжиниринга: игрок видит сцену и цель,
описывает нужный результат на естественном языке, ИИ превращает промт в CSS,
код применяется к сцене в изолированном `iframe`, и автоматический валидатор
проверяет, достигнута ли цель.

## Стек

- **Backend**: ASP.NET Core 9 (Minimal API), один проект `PromptQuest.Web`, раздаёт
  и API, и статику фронтенда.
- **База данных**: PostgreSQL через EF Core 9 (`Npgsql.EntityFrameworkCore.PostgreSQL`).
  В `Development`-окружении по умолчанию включено in-memory хранилище (без БД) —
  переключается конфигурацией, см. «Запуск локально».
- **AI**: Google Gemini через пакет `Google.GenAI`. Модель и ключ берутся из
  конфигурации (`Gemini:Model`, `Gemini:ApiKey`).
- **Фронтенд**: чистый HTML/CSS/JavaScript (ES-модули), без фреймворков и сборщиков.
- **Движок валидации**: sandbox-`iframe` (`sandbox="allow-scripts"`, без
  `allow-same-origin`) + протокол `postMessage` — код игрока и ответ ИИ никогда не
  выполняются на сервере и не имеют доступа к DOM/хранилищам родительской страницы.

## Запуск локально

### Вариант 1 — без базы данных (in-memory, для разработки)

```
dotnet run --project PromptQuest.Web
```

В окружении `Development` (используется по умолчанию при `dotnet run`) приложение
читает `appsettings.Development.json`, где `PromptQuest:UseInMemoryStorage=true` —
игроки, попытки и прогресс хранятся в памяти процесса и теряются при перезапуске.
Подключение к БД в этом режиме не требуется.

Открыть `http://localhost:5280` (профиль `http` из `launchSettings.json`).

### Вариант 2 — с PostgreSQL

Нужны переменные окружения (значения ниже — только имена, не реальные секреты):

```
ConnectionStrings__Default=Host=<хост>;Database=<имя БД>;Username=<пользователь>;Password=<пароль>
Gemini__ApiKey=<ключ Gemini API>
Gemini__Model=<имя модели, например gemini-3.1-flash-lite>
```

Строка подключения проверяется при старте: `Host`, `Database` и `Username` должны
быть непустыми, иначе приложение завершится с ошибкой конфигурации при запуске.
Этот режим используется всегда, когда `PromptQuest:UseInMemoryStorage` не включён
(то есть всегда вне `Development`, и в `Development`, если явно выключить флаг).

Применить миграции перед первым запуском:

```
dotnet ef database update --project PromptQuest.Web
```

(требует `dotnet-ef`: `dotnet tool install --global dotnet-ef`, если не установлен).
Схема — три таблицы: `Players`, `Attempts`, `LevelProgresses`; миграция лежит в
`PromptQuest.Web/Migrations/`.

**Секреты никогда не хранить в `appsettings.json`** — только через переменные
окружения или `dotnet user-secrets`. На момент последнего аудита в
`PromptQuest.Web/appsettings.json` в репозитории был обнаружен закоммиченный ключ
Gemini API — это нужно устранить отдельно (ротация ключа + удаление из текущего
файла и из истории git), эта работа не входит в данное обновление документации.

### Тесты

В решении нет проекта с автоматическими тестами — `dotnet test` не применим.
Единственная доступная проверка — `dotnet build PromptQuest.sln`.

## Игровой цикл

Ввод никнейма → список уровней → экран игры (сцена в `iframe` + поле промта,
результат ИИ применяется автоматически) → таблица лидеров.

Промт игрока отправляется ИИ, который возвращает CSS-правила для контейнера
`#pond`; результат применяется к сцене, и раннер внутри `iframe` проверяет,
достигнута ли цель уровня. Ручной ввод CSS (без ИИ) остаётся в коде как
альтернативный режим, управляемый флагом конфигурации `ManualCodeEntry`
(по умолчанию выключен — активен режим с ИИ).

### Как ИИ получает задачу

На каждый промт игрока сервис генерации кода (`PromptQuest.Web/AI/AiAgent.cs`)
обращается к Gemini с системной инструкцией и текстом промта игрока. Игроку
видимые `goal`/`hint` конкретного уровня в запрос к модели не передаются ни в
каком виде — модель не может решить уровень по описанию цели, минуя промт
игрока; это намеренное ограничение, подробности — в `SPEC-ADDENDUM-01.md`.

## Уровни

Все уровни — CSS-геометрические задачи: игрок описывает, где на сцене (внутри
`#pond`, где находятся лягушка `#frog`/`.frog` и кувшинка `#lily`/`.lily`) должен
оказаться элемент, ИИ пишет CSS для `#pond`, валидатор сравнивает фактическое и
ожидаемое положение прямоугольников на экране.

| ID | Задача | Проверка |
|---|---|---|
| `css-01-justify` | Лягушка должна оказаться у правого края пруда | `overlapCenter` |
| `css-02-align` | Лягушка должна опуститься к нижнему краю пруда | `overlapCenter` |
| `css-03-center` | Лягушка должна оказаться в центре пруда | `overlapCenter` |
| `css-04-reverse` | Три лягушки — на кувшинках своего цвета в зеркальном порядке | 3× `overlapCenter` |
| `css-05-spread` | Три лягушки равномерно по ширине, крайние у краёв | 3× `overlapCenter` |
| `css-06-grid` | Лягушка должна попасть в правую нижнюю клетку сетки 3×3 | `containedIn` |

Планируется, что новые уровни также будут CSS-ориентированными (без уровней на
CSS-селекторы и JavaScript).

Движок валидации (`wwwroot/sandbox/runner.js`) умеет оценивать 9 видов проверок
(`overlapCenter`, `containedIn`, `orderX`, `orderY`, `computedStyle`,
`selectorMatches`, `textContent`, `classOnElements`, `elementCount`) — текущий
каталог уровней использует только `overlapCenter` и `containedIn`, остальные
виды реализованы и доступны для будущих уровней, но сейчас ничем не задействованы.

## Структура репозитория

### Корень

| Путь | Назначение |
|---|---|
| `PromptQuest.sln` | Решение Visual Studio, один проект. |
| `README.md` | Этот файл. |
| `SPEC.md` | Описание архитектуры и контрактов системы. |
| `SPEC-ADDENDUM-01.md` | Принцип изоляции `goal`/`hint` от ИИ и правила формулировки уровней. |
| `SPEC-ADDENDUM-02.md` | Принцип «скрытой информации» в разметке сцены. |
| `.gitignore` / `.gitattributes` | Стандартные игнор-правила и нормализация конца строк. |

### `PromptQuest.Web/`

| Путь | Назначение |
|---|---|
| `Program.cs` | DI-регистрация хранилищ (in-memory или EF/Postgres — выбор по конфигурации), регистрация Gemini-клиента, подключение статики и эндпоинтов. |
| `appsettings.json` / `appsettings.Development.json` | Конфигурация `PromptQuest`, `Gemini`; `UseInMemoryStorage` в Development. |
| `AI/AiAgent.cs` | Реализация `ICodeGenerationService` поверх Gemini. |
| `Configuration/AppOptions.cs` | Типизированная модель секции `PromptQuest`. |
| `Models/` | `Player`, `LevelProgress`, `Attempt` (+ `CodeSource`), `LevelDefinition`, `LevelScene`, `LevelValidation`, `LevelCheck` — модель данных и схема уровня. |
| `Dtos/` | Контракты HTTP API (camelCase JSON). |
| `Services/ICodeGenerationService.cs`, `ScoreCalculator.cs`, `JsonLevelStore.cs`, `ManualCodeGenerationService.cs` | Интерфейс генерации кода, расчёт очков на сервере, чтение `Data/levels.json`, неиспользуемая сейчас заглушка ручного ввода. |
| `Services/Storage/` | Интерфейсы `IPlayerStore`, `IAttemptStore`, `ILevelStore`; `Storage/Db/` — EF/Postgres-реализации (`AppDbContext`, `EfPlayerStore`, `EfAttemptStore`, `EfLeaderboardService`); `Storage/InMemory/` — реализации для режима без БД. |
| `Migrations/` | Единственная миграция EF Core (`InitialCreate`): таблицы `Players`, `Attempts`, `LevelProgresses`. |
| `Endpoints/` | Minimal API: игроки, уровни, попытки, таблица лидеров. |
| `Data/levels.json` | Каталог уровней (сейчас 6 CSS-уровней). |
| `wwwroot/` | Фронтенд: `index.html`, `css/app.css`, `js/*.js` (роутинг, экраны, таймер, обёртка над `fetch`), `sandbox/runner.html` + `runner.js` (раннер внутри `iframe`). |

## Известные ограничения

- **Нет автоматических тестов и CI** — только ручная проверка сборки.
- **Нет Dockerfile/деплой-конфигурации** — способ хостинга не определён.
- Код игрока и ответ ИИ выполняются только в браузере игрока, в изолированном
  `iframe`; результат проверки отправляется на сервер клиентом — это осознанное
  доверие клиенту, серверной переверификации результата нет.
- Данные в режиме in-memory (`Development` по умолчанию) теряются при каждом
  перезапуске процесса.
- В `appsettings.json` на момент последнего аудита обнаружен закоммиченный ключ
  Gemini API — требуется ротация ключа и его удаление из репозитория и истории
  git; это не исправлено в рамках текущего обновления документации.
