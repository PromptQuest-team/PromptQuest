const PLAYER_ID_KEY = "pq.playerId";
const NICKNAME_KEY = "pq.nickname";

export const State = {
  getPlayerId() {
    return localStorage.getItem(PLAYER_ID_KEY);
  },
  getNickname() {
    return localStorage.getItem(NICKNAME_KEY);
  },
  setPlayer(playerId, nickname) {
    localStorage.setItem(PLAYER_ID_KEY, playerId);
    localStorage.setItem(NICKNAME_KEY, nickname);
  },
  clearPlayer() {
    localStorage.removeItem(PLAYER_ID_KEY);
    localStorage.removeItem(NICKNAME_KEY);
  },
};
