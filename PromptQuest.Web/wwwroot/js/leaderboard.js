import { Api } from "./api.js";
import { State } from "./state.js";
import { formatMs } from "./timer.js";

function escapeHtml(value) {
  const div = document.createElement("div");
  div.textContent = value ?? "";
  return div.innerHTML;
}

function renderRows(entries) {
  const nickname = State.getNickname();

  if (entries.length === 0) {
    return `<tr><td colspan="5" class="empty-row">Пока никто не прошёл ни одного уровня.</td></tr>`;
  }

  return entries
    .map((entry) => {
      const isCurrent = entry.nickname === nickname;
      return `
        <tr class="${isCurrent ? "current-player" : ""}">
          <td>${entry.rank}</td>
          <td>${escapeHtml(entry.nickname)}</td>
          <td>${entry.attempts}</td>
          <td>${formatMs(entry.timeMs)}</td>
          <td>${entry.score}</td>
        </tr>
      `;
    })
    .join("");
}

export const LeaderboardScreen = {
  async render(container) {
    container.innerHTML = `
      <section class="screen screen-leaderboard">
        <h2>Таблица лидеров</h2>
        <div class="tabs">
          <button type="button" class="tab-button active" data-tab="global">Общий зачёт</button>
          <button type="button" class="tab-button" data-tab="level">По уровню</button>
        </div>
        <div class="tab-panel" id="tab-global">
          <table class="leaderboard-table">
            <thead>
              <tr><th>Место</th><th>Никнейм</th><th>Попытки</th><th>Время</th><th>Очки</th></tr>
            </thead>
            <tbody id="global-rows"><tr><td colspan="5">Загрузка…</td></tr></tbody>
          </table>
        </div>
        <div class="tab-panel" id="tab-level" hidden>
          <select id="level-select"></select>
          <table class="leaderboard-table">
            <thead>
              <tr><th>Место</th><th>Никнейм</th><th>Попытки</th><th>Время</th><th>Очки</th></tr>
            </thead>
            <tbody id="level-rows"><tr><td colspan="5">Выберите уровень</td></tr></tbody>
          </table>
        </div>
      </section>
    `;

    const tabButtons = container.querySelectorAll(".tab-button");
    const tabGlobal = container.querySelector("#tab-global");
    const tabLevel = container.querySelector("#tab-level");

    tabButtons.forEach((button) => {
      button.addEventListener("click", () => {
        tabButtons.forEach((b) => b.classList.remove("active"));
        button.classList.add("active");
        const isGlobal = button.dataset.tab === "global";
        tabGlobal.hidden = !isGlobal;
        tabLevel.hidden = isGlobal;
      });
    });

    const globalRows = container.querySelector("#global-rows");
    try {
      const entries = await Api.getGlobalLeaderboard(20);
      globalRows.innerHTML = renderRows(entries);
    } catch (err) {
      globalRows.innerHTML = `<tr><td colspan="5">Не удалось загрузить: ${escapeHtml(err.message)}</td></tr>`;
    }

    const levelSelect = container.querySelector("#level-select");
    const levelRows = container.querySelector("#level-rows");

    try {
      const levels = await Api.getLevels(State.getPlayerId());
      levelSelect.innerHTML = levels
        .map((level) => `<option value="${escapeHtml(level.id)}">${level.order}. ${escapeHtml(level.title)}</option>`)
        .join("");

      async function loadLevelLeaderboard() {
        const levelId = levelSelect.value;
        if (!levelId) {
          return;
        }
        levelRows.innerHTML = "<tr><td colspan=\"5\">Загрузка…</td></tr>";
        try {
          const entries = await Api.getLevelLeaderboard(levelId, 20);
          levelRows.innerHTML = renderRows(entries);
        } catch (err) {
          levelRows.innerHTML = `<tr><td colspan="5">Не удалось загрузить: ${escapeHtml(err.message)}</td></tr>`;
        }
      }

      levelSelect.addEventListener("change", loadLevelLeaderboard);
      if (levels.length > 0) {
        await loadLevelLeaderboard();
      }
    } catch (err) {
      levelRows.innerHTML = `<tr><td colspan="5">Не удалось загрузить уровни: ${escapeHtml(err.message)}</td></tr>`;
    }

    return null;
  },
};
