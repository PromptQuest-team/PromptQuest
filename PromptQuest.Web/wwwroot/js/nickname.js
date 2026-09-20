import { Api } from "./api.js";
import { State } from "./state.js";

export const NicknameScreen = {
  render(container) {
    container.innerHTML = `
      <section class="screen screen-nickname">
        <h2>Добро пожаловать в PromptQuest</h2>
        <p>Введите никнейм, чтобы начать играть.</p>
        <form id="nickname-form" class="nickname-form">
          <input
            type="text"
            id="nickname-input"
            minlength="2"
            maxlength="20"
            placeholder="Никнейм"
            autocomplete="off"
            required
          />
          <button type="submit">Играть</button>
        </form>
        <p class="error-message" id="nickname-error" hidden></p>
      </section>
    `;

    const form = container.querySelector("#nickname-form");
    const input = container.querySelector("#nickname-input");
    const errorEl = container.querySelector("#nickname-error");

    input.focus();

    form.addEventListener("submit", async (event) => {
      event.preventDefault();
      errorEl.hidden = true;

      const nickname = input.value.trim();
      if (nickname.length < 2 || nickname.length > 20) {
        errorEl.textContent = "Никнейм должен содержать от 2 до 20 символов.";
        errorEl.hidden = false;
        return;
      }

      try {
        const player = await Api.createPlayer(nickname);
        State.setPlayer(player.playerId, player.nickname);
        location.hash = "#/levels";
      } catch (err) {
        errorEl.textContent = err.message || "Не удалось создать игрока.";
        errorEl.hidden = false;
      }
    });

    return null;
  },
};
