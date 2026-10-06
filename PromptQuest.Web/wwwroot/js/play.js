import { Api } from "./api.js";
import { State } from "./state.js";
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
        </div>
        <div class="play-columns">
          <div class="play-left">
            <h3>Цель</h3>
            <p id="play-goal">${escapeHtml(level.goal)}</p>
            <div id="scene-container" class="scene-container"></div>
            <p class="hint">${escapeHtml(level.hint)}</p>
            <button type="button" id="reset-button" class="secondary-button">Сбросить уровень</button>
          </div>
          <div class="play-right">
            <h3>Промт</h3>
            <textarea id="prompt-input" rows="4" placeholder="Опишите результат, который должен получиться…" ${
              manualEntry ? "disabled" : ""
            }></textarea>
            <p class="char-counter">Длина промта: <span id="prompt-char-count">0</span> символов</p>
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
    const promptInput = container.querySelector("#prompt-input");
    const promptCharCount = container.querySelector("#prompt-char-count");
    const submitPromptButton = container.querySelector("#submit-prompt-button");
    const codeInput = container.querySelector("#code-input");
    const runButton = container.querySelector("#run-button");
    const hintEl = container.querySelector("#play-hint");
    const checksList = container.querySelector("#checks-list");
    const victoryPanel = container.querySelector("#victory-panel");
    const resetButton = container.querySelector("#reset-button");

    let passed = false;

    function updateCharCount() {
      promptCharCount.textContent = String(promptInput.value.trim().length);
    }

    promptInput.addEventListener("input", updateCharCount);
    updateCharCount();

    function showHint(text) {
      hintEl.textContent = text;
      hintEl.hidden = false;
    }

    function clearHint() {
      hintEl.hidden = true;
    }

    function describeAttemptError(err) {
      if (err && err.status === 429) {
        const seconds = err.retryAfterSeconds || 60;
        return `Слишком часто, подождите ${seconds} секунд.`;
      }
      if (err && err.status === 502) {
        return "ИИ временно недоступен, попробуйте ещё раз.";
      }
      return (err && err.message) || "Не удалось зарегистрировать попытку.";
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
        <p>Решено промтом из ${result.promptLength} символов${
          result.personalBest ? " (новый личный рекорд!)" : ""
        }</p>
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
      }

      try {
        const result = await Api.submitResult(attemptId, {
          passed: runResult.passed,
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
        showHint(describeAttemptError(err));
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
        // 429 (лимит запросов) и 502 (сбой ИИ) — попытка на сервере не создана,
        // валидатор не запускаем, просто показываем сообщение.
        showHint(describeAttemptError(err));
      } finally {
        submitPromptButton.disabled = false;
      }
    });

    resetButton.addEventListener("click", async () => {
      passed = false;
      checksList.innerHTML = "";
      victoryPanel.hidden = true;
      victoryPanel.innerHTML = "";
      clearHint();
      codeInput.value = level.codeTemplate;
      await showResetScene(sceneContainer, level);
    });

    await showResetScene(sceneContainer, level);

    return null;
  },
};
