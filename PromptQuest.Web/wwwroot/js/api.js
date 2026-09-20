const BASE = "/api";

async function request(method, path, body) {
  const res = await fetch(BASE + path, {
    method,
    headers: body !== undefined ? { "Content-Type": "application/json" } : undefined,
    body: body !== undefined ? JSON.stringify(body) : undefined,
  });

  if (!res.ok) {
    let detail = `Ошибка запроса (${res.status})`;
    try {
      const problem = await res.json();
      if (problem && problem.detail) {
        detail = problem.detail;
      }
    } catch {
      // тело ответа не в формате ProblemDetails — используем сообщение по умолчанию
    }
    throw new Error(detail);
  }

  if (res.status === 204) {
    return null;
  }
  return res.json();
}

export const Api = {
  createPlayer(nickname) {
    return request("POST", "/players", { nickname });
  },
  getPlayer(playerId) {
    return request("GET", `/players/${encodeURIComponent(playerId)}`);
  },
  getLevels(playerId) {
    const query = playerId ? `?playerId=${encodeURIComponent(playerId)}` : "";
    return request("GET", `/levels${query}`);
  },
  getLevel(levelId) {
    return request("GET", `/levels/${encodeURIComponent(levelId)}`);
  },
  createAttempt(playerId, levelId, prompt) {
    return request("POST", "/attempts", { playerId, levelId, prompt });
  },
  submitResult(attemptId, payload) {
    return request("POST", `/attempts/${encodeURIComponent(attemptId)}/result`, payload);
  },
  getLevelLeaderboard(levelId, take) {
    return request(
      "GET",
      `/leaderboard/levels/${encodeURIComponent(levelId)}?take=${take}`
    );
  },
  getGlobalLeaderboard(take) {
    return request("GET", `/leaderboard/global?take=${take}`);
  },
};
