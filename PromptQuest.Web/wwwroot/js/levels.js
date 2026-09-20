import { Api } from "./api.js";
import { State } from "./state.js";
import { formatMs } from "./timer.js";

function escapeHtml(value) {
  const div = document.createElement("div");
  div.textContent = value ?? "";
  return div.innerHTML;
}

const CATEGORY_LABELS = {
  css: "CSS",
  selector: "Селекторы",
  js: "JavaScript",
};

export const LevelsScreen = {
  async render(container) {
    container.innerHTML = `
      <section class="screen screen-levels">
        <h2>Уровни</h2>
        <div class="level-grid" id="level-grid">Загрузка…</div>
      </section>
    `;

    const grid = container.querySelector("#level-grid");

    try {
      const levels = await Api.getLevels(State.getPlayerId());
      grid.innerHTML = "";

      for (const level of levels) {
        const card = document.createElement("a");
        card.href = `#/play/${encodeURIComponent(level.id)}`;
        card.className = "level-card" + (level.completed ? " completed" : "");

        const categoryLabel = CATEGORY_LABELS[level.category] || level.category;
        const bestHtml = level.completed
          ? `<div class="level-card-best">Лучшее: ${level.bestAttempts} поп., ${formatMs(
              level.bestTimeMs
            )}, ${level.bestScore} очк.</div>`
          : "";

        card.innerHTML = `
          <div class="level-card-header">
            <span class="level-card-order">Уровень ${level.order}</span>
            ${level.completed ? '<span class="level-card-badge">пройден</span>' : ""}
          </div>
          <div class="level-card-title">${escapeHtml(level.title)}</div>
          <div class="level-card-meta">${escapeHtml(categoryLabel)} · сложность ${level.difficulty}</div>
          ${bestHtml}
        `;

        grid.appendChild(card);
      }

      if (levels.length === 0) {
        grid.textContent = "Уровни не найдены.";
      }
    } catch (err) {
      grid.textContent = "Не удалось загрузить уровни: " + err.message;
    }

    return null;
  },
};
