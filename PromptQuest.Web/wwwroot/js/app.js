import { State } from "./state.js";
import { Api } from "./api.js";
import { NicknameScreen } from "./nickname.js";
import { LevelsScreen } from "./levels.js";
import { PlayScreen } from "./play.js";
import { LeaderboardScreen } from "./leaderboard.js";

const appEl = document.getElementById("app");
const navEl = document.getElementById("main-nav");
const changeNicknameLink = document.getElementById("nav-change-nickname");

let unmountCurrent = null;
let playerVerified = false;

function updateNav() {
  const hasPlayer = Boolean(State.getPlayerId());
  navEl.hidden = !hasPlayer;
}

// Фаза 1 хранит игроков в памяти процесса: перезапуск сервера стирает всех
// игроков, но playerId в localStorage браузера переживает перезапуск. Без этой
// проверки приложение считало бы такой playerId валидным до первого запроса,
// который упал бы с 404 "игрок не найден". Проверяем один раз за сессию вкладки.
async function ensurePlayerIsValid() {
  const playerId = State.getPlayerId();
  if (!playerId || playerVerified) {
    return;
  }

  try {
    await Api.getPlayer(playerId);
    playerVerified = true;
  } catch {
    State.clearPlayer();
  }
}

async function route() {
  if (unmountCurrent) {
    unmountCurrent();
    unmountCurrent = null;
  }

  await ensurePlayerIsValid();

  const hash = location.hash || "#/nickname";
  const playerId = State.getPlayerId();

  if (!playerId && hash !== "#/nickname") {
    location.hash = "#/nickname";
    return;
  }

  if (playerId && hash === "#/nickname") {
    location.hash = "#/levels";
    return;
  }

  updateNav();
  appEl.innerHTML = "";

  if (hash === "#/nickname") {
    unmountCurrent = (await NicknameScreen.render(appEl)) || null;
  } else if (hash === "#/levels") {
    unmountCurrent = (await LevelsScreen.render(appEl)) || null;
  } else if (hash.startsWith("#/play/")) {
    const levelId = decodeURIComponent(hash.slice("#/play/".length));
    unmountCurrent = (await PlayScreen.render(appEl, levelId)) || null;
  } else if (hash === "#/leaderboard") {
    unmountCurrent = (await LeaderboardScreen.render(appEl)) || null;
  } else {
    location.hash = "#/levels";
  }
}

changeNicknameLink.addEventListener("click", (event) => {
  event.preventDefault();
  State.clearPlayer();
  playerVerified = false;
  if (location.hash === "#/nickname") {
    route();
  } else {
    location.hash = "#/nickname";
  }
});

window.addEventListener("hashchange", route);
window.addEventListener("DOMContentLoaded", route);
