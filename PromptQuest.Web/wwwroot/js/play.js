import { Api } from "./api.js";
import { State } from "./state.js";
import { LevelTimer, formatMs } from "./timer.js";
import { runInSandbox, showResetScene } from "./sandbox.js";

function escapeHtml(value) {
  const div = document.createElement("div");
  div.textContent = value ?? "";
  return div.innerHTML;
}

export const PlayScreen = {
  async render(container, levelId) {
    container.innerHTML = `<section class="screen screen-play"><p>Загрузка уровня…</p></section>`;

    let level;
    let levelIndex = 0;
    let levelCount = 0;

    try {
      [level] = await Promise.all([Api.getLevel(levelId)]);
      const summaries = await Api.getLevels(State.getPlayerId());
      levelCount = summaries.length;
      const idx = summaries.findIndex((l) => l.id === levelId);
      levelIndex = idx >= 0 ? idx + 1 : level.order;
    } catch (err) {
      container.innerHTML = `<section class="screen screen-play"><p class="error-message">Не удалось загрузить уровень: ${escapeHtml(
        err.message
      )}</p></section>`;
      return null;
    }

    const playerId = State.getPlayerId();
    const manualEntry = level.manualEntry;

    container.innerHTML = `
      <section class="screen screen-play">
        <div class="play-topbar">
          <span>Уровень ${levelIndex} / ${levelCount}</span>
          <span>Время <span id="play-timer">00:00</span></span>
          <span>Попыток: <span id="play-attempts">0</span></span>
        </div>
        <div class="play-columns">
          <div class="play-left">
            <h3>Цель</h3>
            <p id="play-goal">${escapeHtml(level.goal)}</p>
            <div id="scene-container" class="scene-container"></div>
            <p class="hint">Подсказка: ${escapeHtml(level.hint)}</p>
            <button type="button" id="reset-button" class="secondary-button">Сбросить уровень</button>
          </div>
          <div class="play-right">
            <h3>Промт</h3>
            <textarea id="prompt-input" rows="4" placeholder="Опишите результат, который должен получиться…" ${
              manualEntry ? "disabled" : ""
            }></textarea>
            ${manualEntry ? '<p class="mode-note">В этом режиме промт не оценивается — впишите код вручную ниже.</p>' : ""}
            <button type="button" id="submit-prompt-button" class="primary-button" ${manualEntry ? "hidden" : ""}>
              Отправить промт
            </button>

            <h3>Код ${manualEntry ? "" : "от ИИ"}</h3>
            <textarea id="code-input" rows="6" ${manualEntry ? "" : "readonly"}>${escapeHtml(
      level.codeTemplate
    )}</textarea>
            <button type="button" id="run-button" class="primary-button" ${manualEntry ? "" : "hidden"}>
              Запустить код
            </button>
            <p class="error-message" id="play-hint" hidden></p>

            <h3>Результат проверок</h3>
            <ul id="checks-list" class="checks-list"></ul>

            <div id="victory-panel" class="victory-panel" hidden></div>
          </div>
        </div>
      </section>
    `;

    const sceneContainer = container.querySelector("#scene-container");
    const timerEl = container.querySelector("#play-timer");
    const attemptsEl = container.querySelector("#play-attempts");
    const promptInput = container.querySelector("#prompt-input");
    const submitPromptButton = container.querySelector("#submit-prompt-button");
    const codeInput = container.querySelector("#code-input");
    const runButton = container.querySelector("#run-button");
    const hintEl = container.querySelector("#play-hint");
    const checksList = container.querySelector("#checks-list");
    const victoryPanel = container.querySelector("#victory-panel");
    const resetButton = container.querySelector("#reset-button");

    const timer = new LevelTimer();
    timer.start();

    let attemptCount = 0;
    let passed = false;

    const tickInterval = setInterval(() => {
      timerEl.textContent = formatMs(timer.elapsedMs());
    }, 250);

    function showHint(text) {
      hintEl.textContent = text;
      hintEl.hidden = false;
    }

    function clearHint() {
      hintEl.hidden = true;
    }

    function renderChecks(checks) {
      checksList.innerHTML = "";
      for (const check of checks) {
        const li = document.createElement("li");
        li.className = "check-item " + (check.passed ? "check-pass" : "check-fail");
        li.innerHTML = `
          <span class="check-mark">${check.passed ? "✓" : "✗"}</span>
          <span class="check-description">${escapeHtml(check.description)}</span>
          ${
            !check.passed
              ? `<div class="check-detail">ожидалось: ${escapeHtml(check.expected)} — получено: ${escapeHtml(
                  check.actual
                )}</div>`
              : ""
          }
        `;
        checksList.appendChild(li);
      }
    }

    function renderVictory(result) {
      victoryPanel.hidden = false;
      victoryPanel.innerHTML = `
        <h3>Уровень пройден!</h3>
        <p>Попыток: ${result.attempts}</p>
        <p>Время: ${formatMs(timer.elapsedMs())}</p>
        <p>Очки: ${result.score}${result.personalBest ? " — новый личный рекорд!" : ""}</p>
        <div class="victory-actions">
          ${
            result.nextLevelId
              ? `<a class="primary-button" href="#/play/${encodeURIComponent(result.nextLevelId)}">Следующий уровень</a>`
              : ""
          }
          <a class="secondary-button" href="#/levels">К списку</a>
        </div>
      `;
    }

    async function runAttempt(attemptId, code) {
      attemptCount += 1;
      attemptsEl.textContent = String(attemptCount);

      const runResult = await runInSandbox(sceneContainer, level, code);
      renderChecks(runResult.checks);

      if (runResult.error) {
        showHint(
          runResult.error === "timeout"
            ? "Превышено время выполнения кода."
            : `Ошибка выполнения: ${runResult.error}`
        );
      } else {
        clearHint();
      }

      if (runResult.passed) {
        passed = true;
        timer.stop();
      }

      const elapsedMs = timer.elapsedMs();

      try {
        const result = await Api.submitResult(attemptId, {
          passed: runResult.passed,
          elapsedMs,
          code,
          checks: runResult.checks.map((c) => ({ id: c.id, passed: c.passed })),
        });

        if (runResult.passed) {
          renderVictory(result);
        }
      } catch {
        showHint("Не удалось отправить результат на сервер. Попробуйте ещё раз.");
      }
    }

    runButton.addEventListener("click", async () => {
      if (passed) {
        return;
      }

      const code = codeInput.value.trim();
      if (code === "") {
        showHint("Введите код перед запуском.");
        return;
      }

      runButton.disabled = true;
      clearHint();

      try {
        const attempt = await Api.createAttempt(playerId, levelId, promptInput.value.trim());
        await runAttempt(attempt.attemptId, code);
      } catch (err) {
        showHint(err.message || "Не удалось зарегистрировать попытку.");
      } finally {
        runButton.disabled = false;
      }
    });

    submitPromptButton.addEventListener("click", async () => {
      if (passed) {
        return;
      }

      const prompt = promptInput.value.trim();
      submitPromptButton.disabled = true;
      clearHint();

      try {
        const attempt = await Api.createAttempt(playerId, levelId, prompt);
        codeInput.value = attempt.code;
        await runAttempt(attempt.attemptId, attempt.code);
      } catch (err) {
        showHint(err.message || "Не удалось получить код.");
      } finally {
        submitPromptButton.disabled = false;
      }
    });

    resetButton.addEventListener("click", async () => {
      passed = false;
      attemptCount = 0;
      attemptsEl.textContent = "0";
      timer.start();
      timerEl.textContent = formatMs(0);
      checksList.innerHTML = "";
      victoryPanel.hidden = true;
      victoryPanel.innerHTML = "";
      clearHint();
      codeInput.value = level.codeTemplate;
      await showResetScene(sceneContainer, level);
    });

    await showResetScene(sceneContainer, level);

    return () => {
      clearInterval(tickInterval);
      timer.dispose();
    };
  },
};
