import { Api } from "./api.js";
import { State } from "./state.js";

function escapeHtml(value) {
  const div = document.createElement("div");
  div.textContent = value ?? "";
  return div.innerHTML;
}

function renderRows(entries) {
  const nickname = State.getNickname();

  if (entries.length === 0) {
    return `<tr><td colspan="3" class="empty-row">Пока никто не прошёл этот уровень.</td></tr>`;
  }

  return entries
    .map((entry) => {
      const isCurrent = entry.nickname === nickname;
      return `
        <tr class="${isCurrent ? "current-player" : ""}">
          <td>${entry.rank}</td>
          <td>${escapeHtml(entry.nickname)}</td>
          <td>${entry.promptLength}</td>
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
        <select id="level-select"></select>
        <table class="leaderboard-table">
          <thead>
            <tr><th>Место</th><th>Никнейм</th><th>Длина промта</th></tr>
          </thead>
          <tbody id="level-rows"><tr><td colspan="3">Выберите уровень</td></tr></tbody>
        </table>
      </section>
    `;

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
        levelRows.innerHTML = "<tr><td colspan=\"3\">Загрузка…</td></tr>";
        try {
          const entries = await Api.getLevelLeaderboard(levelId, 20);
          levelRows.innerHTML = renderRows(entries);
        } catch (err) {
          levelRows.innerHTML = `<tr><td colspan="3">Не удалось загрузить: ${escapeHtml(err.message)}</td></tr>`;
        }
      }

      levelSelect.addEventListener("change", loadLevelLeaderboard);
      if (levels.length > 0) {
        await loadLevelLeaderboard();
      }
    } catch (err) {
      levelRows.innerHTML = `<tr><td colspan="3">Не удалось загрузить уровни: ${escapeHtml(err.message)}</td></tr>`;
    }

    return null;
  },
};
