// Создание sandbox-iframe и протокол обмена сообщениями (раздел 9 ТЗ).
// iframe пересоздаётся перед каждым прогоном (следующий вызов заменяет предыдущий
// через container.innerHTML) — чистое состояние сцены. После получения результата
// iframe НЕ удаляется — он остаётся на экране, показывая игроку сцену в том виде,
// в котором её оценивали проверки. Исключение — таймаут: зависший прогон удаляется
// немедленно, чтобы не тратить ресурсы на бесконечный цикл.
export function runInSandbox(container, level, code) {
  return new Promise((resolve) => {
    const runId = crypto.randomUUID();

    const iframe = document.createElement("iframe");
    iframe.setAttribute("sandbox", "allow-scripts");
    iframe.className = "sandbox-frame";
    iframe.src = "sandbox/runner.html";

    let settled = false;
    let watchdog = null;

    function cleanup() {
      if (watchdog !== null) {
        clearTimeout(watchdog);
        watchdog = null;
      }
      window.removeEventListener("message", onMessage);
    }

    function finish(result) {
      if (settled) {
        return;
      }
      settled = true;
      cleanup();
      if (result.timedOut && iframe.parentNode) {
        iframe.parentNode.removeChild(iframe);
        const message = document.createElement("div");
        message.className = "sandbox-timeout-message";
        message.textContent = "Превышено время выполнения кода — сцена остановлена.";
        container.innerHTML = "";
        container.appendChild(message);
      }
      resolve(result);
    }

    function onMessage(event) {
      if (event.source !== iframe.contentWindow) {
        return;
      }

      const msg = event.data;
      if (!msg) {
        return;
      }

      if (msg.type === "RUN_RESULT" && msg.runId === runId && msg.size) {
        // Каждый уровень определяет свой размер сцены в levels.json — раннер
        // измеряет фактический размер и сообщает его здесь, вместо того
        // чтобы держать один фиксированный размер iframe для всех уровней
        // (иначе уровни меньше/больше этого размера показывают белые полосы
        // или обрезаются).
        iframe.style.width = `${msg.size.width}px`;
        iframe.style.height = `${msg.size.height}px`;
      }

      if (msg.type === "RUNNER_READY") {
        iframe.contentWindow.postMessage(
          {
            type: "RUN",
            runId,
            injectionMode: level.injectionMode,
            scene: level.scene,
            code,
            validation: level.validation,
            forbiddenPatterns: level.forbiddenPatterns,
          },
          "*"
        );
        return;
      }

      if (msg.type === "RUN_RESULT" && msg.runId === runId) {
        finish({ passed: msg.passed, checks: msg.checks || [], error: msg.error || null, timedOut: false });
      }
    }

    window.addEventListener("message", onMessage);

    // Сторожевой таймер: если результат не пришёл за timeoutMs, iframe удаляется,
    // прогон засчитывается как неуспешный с причиной timeout.
    const timeoutMs = (level.validation && level.validation.timeoutMs) || 2000;
    watchdog = setTimeout(() => {
      finish({ passed: false, checks: [], error: "timeout", timedOut: true });
    }, timeoutMs);

    container.innerHTML = "";
    container.appendChild(iframe);
  });
}

// Показывает исходное состояние сцены без выполнения кода игрока (для «Сбросить уровень»).
export function showResetScene(container, level) {
  return runInSandbox(container, { ...level, validation: { ...level.validation, checks: [] } }, "");
}
