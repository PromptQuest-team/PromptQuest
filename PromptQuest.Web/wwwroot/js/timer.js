// Таймер уровня. Использует performance.now() (монотонные часы), не Date.now().
export class LevelTimer {
  constructor() {
    this._elapsed = 0;
    this._segmentStart = null;
    this._running = false;
    this._onVisibility = () => {
      if (document.hidden) {
        this.pause();
      } else {
        this.resume();
      }
    };
  }

  start() {
    this._elapsed = 0;
    this._segmentStart = performance.now();
    this._running = true;
    document.addEventListener("visibilitychange", this._onVisibility);
  }

  pause() {
    if (!this._running) {
      return;
    }
    this._elapsed += performance.now() - this._segmentStart;
    this._running = false;
  }

  resume() {
    if (this._running) {
      return;
    }
    this._segmentStart = performance.now();
    this._running = true;
  }

  stop() {
    this.pause();
    document.removeEventListener("visibilitychange", this._onVisibility);
    return this.elapsedMs();
  }

  dispose() {
    document.removeEventListener("visibilitychange", this._onVisibility);
  }

  elapsedMs() {
    if (this._running) {
      return Math.round(this._elapsed + (performance.now() - this._segmentStart));
    }
    return Math.round(this._elapsed);
  }
}

export function formatMs(ms) {
  const totalSeconds = Math.floor(ms / 1000);
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;
}
