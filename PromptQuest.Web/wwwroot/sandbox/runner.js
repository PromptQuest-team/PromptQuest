// Раннер выполняется внутри sandbox-iframe (sandbox="allow-scripts", без allow-same-origin).
// Это единственное место, где выполняется код игрока и реализованы виды проверок.

const sceneRoot = document.getElementById("scene-root");
const baseCssEl = document.getElementById("base-css");

function q(root, selector) {
  try {
    return root.querySelector(selector);
  } catch {
    return null;
  }
}

function qAll(root, selector) {
  try {
    return Array.from(root.querySelectorAll(selector));
  } catch {
    return [];
  }
}

function centerOf(rect) {
  return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
}

function fmtPoint(p) {
  return `центр (${Math.round(p.x)}, ${Math.round(p.y)})`;
}

function fmtRect(r) {
  return `прямоугольник (${Math.round(r.left)}, ${Math.round(r.top)}) — (${Math.round(r.right)}, ${Math.round(r.bottom)})`;
}

// Словарь функций (check, ctx) => CheckResult. kind сопоставляется с ключом словаря.
const CHECKS = {
  overlapCenter(check, ctx) {
    const subject = q(ctx.sceneRoot, check.subject);
    const target = q(ctx.sceneRoot, check.target);
    if (!subject || !target) {
      return failResult(check, "оба элемента найдены", "один из элементов отсутствует на сцене");
    }

    const subjectCenter = centerOf(subject.getBoundingClientRect());
    const targetCenter = centerOf(target.getBoundingClientRect());
    const passed =
      Math.abs(subjectCenter.x - targetCenter.x) <= ctx.tolerancePx &&
      Math.abs(subjectCenter.y - targetCenter.y) <= ctx.tolerancePx;

    return {
      id: check.id,
      passed,
      description: check.description || check.id,
      expected: fmtPoint(targetCenter),
      actual: fmtPoint(subjectCenter),
    };
  },

  containedIn(check, ctx) {
    const subject = q(ctx.sceneRoot, check.subject);
    const target = q(ctx.sceneRoot, check.target);
    if (!subject || !target) {
      return failResult(check, "оба элемента найдены", "один из элементов отсутствует на сцене");
    }

    const s = subject.getBoundingClientRect();
    const t = target.getBoundingClientRect();
    const tol = ctx.tolerancePx;
    const passed =
      s.left >= t.left - tol &&
      s.top >= t.top - tol &&
      s.right <= t.right + tol &&
      s.bottom <= t.bottom + tol;

    return {
      id: check.id,
      passed,
      description: check.description || check.id,
      expected: fmtRect(t),
      actual: fmtRect(s),
    };
  },

  noOverlap(check, ctx) {
    const subject = q(ctx.sceneRoot, check.subject);
    const target = q(ctx.sceneRoot, check.target);
    if (!subject || !target) {
      return failResult(check, "оба элемента найдены", "один из элементов отсутствует на сцене");
    }

    const s = subject.getBoundingClientRect();
    const t = target.getBoundingClientRect();
    const intersects = s.left < t.right && s.right > t.left && s.top < t.bottom && s.bottom > t.top;

    return {
      id: check.id,
      passed: !intersects,
      description: check.description || check.id,
      expected: "прямоугольники не пересекаются",
      actual: intersects
        ? `пересекаются: ${fmtRect(s)} и ${fmtRect(t)}`
        : `не пересекаются: ${fmtRect(s)} и ${fmtRect(t)}`,
    };
  },

  orderX(check, ctx) {
    return orderCheck(check, ctx, "x");
  },

  orderY(check, ctx) {
    return orderCheck(check, ctx, "y");
  },

  computedStyle(check, ctx) {
    const subject = q(ctx.sceneRoot, check.subject);
    if (!subject) {
      return failResult(check, String(check.expected), "элемент отсутствует на сцене");
    }

    const actual = String(getComputedStyle(subject)[check.property] ?? "").trim();
    const expected = String(check.expected ?? "").trim();

    return {
      id: check.id,
      passed: actual === expected,
      description: check.description || check.id,
      expected,
      actual,
    };
  },

  selectorMatches(check, ctx) {
    const matched = ctx.playerSelection ? Array.from(ctx.playerSelection) : [];
    const matchedIds = matched.map((el) => el.id).filter(Boolean);
    const matchedSet = new Set(matchedIds);
    const expectedIds = check.expectedIds || [];
    const expectedSet = new Set(expectedIds);

    const passed =
      matchedSet.size === expectedSet.size &&
      expectedIds.every((id) => matchedSet.has(id));

    return {
      id: check.id,
      passed,
      description: check.description || check.id,
      expected: expectedIds.join(", ") || "(ничего)",
      actual: matchedIds.join(", ") || "(ничего)",
    };
  },

  textContent(check, ctx) {
    const subject = q(ctx.sceneRoot, check.subject);
    if (!subject) {
      return failResult(check, String(check.expected ?? ""), "элемент отсутствует на сцене");
    }

    const actual = (subject.textContent || "").trim();
    const expected = String(check.expected ?? "").trim();

    return {
      id: check.id,
      passed: actual === expected,
      description: check.description || check.id,
      expected,
      actual,
    };
  },

  classOnElements(check, ctx) {
    const elements = qAll(ctx.sceneRoot, check.selector);
    const expectedIds = new Set(check.expectedIds || []);
    const actualIds = [];
    let passed = true;

    for (const el of elements) {
      const hasClass = el.classList.contains(check.className);
      if (hasClass) {
        actualIds.push(el.id);
      }
      const shouldHaveClass = el.id ? expectedIds.has(el.id) : false;
      if (hasClass !== shouldHaveClass) {
        passed = false;
      }
    }

    return {
      id: check.id,
      passed,
      description: check.description || check.id,
      expected: Array.from(expectedIds).join(", ") || "(ничего)",
      actual: actualIds.join(", ") || "(ничего)",
    };
  },

  elementCount(check, ctx) {
    const count = qAll(ctx.sceneRoot, check.selector).length;
    const expected = Number(check.expected);

    return {
      id: check.id,
      passed: count === expected,
      description: check.description || check.id,
      expected: String(expected),
      actual: String(count),
    };
  },
};

function failResult(check, expected, actual) {
  return {
    id: check.id,
    passed: false,
    description: check.description || check.id,
    expected,
    actual,
  };
}

function orderCheck(check, ctx, axis) {
  const selectors = check.selectors || [];
  const items = selectors.map((selector) => {
    const el = q(ctx.sceneRoot, selector);
    if (!el) {
      return { selector, value: null };
    }
    const center = centerOf(el.getBoundingClientRect());
    return { selector, value: axis === "x" ? center.x : center.y };
  });

  if (items.some((item) => item.value === null)) {
    return failResult(
      check,
      selectors.join(" → "),
      "один из элементов отсутствует на сцене"
    );
  }

  let passed = true;
  for (let i = 1; i < items.length; i++) {
    if (items[i].value <= items[i - 1].value) {
      passed = false;
      break;
    }
  }

  const actualOrder = items
    .slice()
    .sort((a, b) => a.value - b.value)
    .map((item) => item.selector);

  return {
    id: check.id,
    passed,
    description: check.description || check.id,
    expected: selectors.join(" → "),
    actual: actualOrder.join(" → "),
  };
}

function waitTwoFrames() {
  return new Promise((resolve) => {
    requestAnimationFrame(() => {
      requestAnimationFrame(resolve);
    });
  });
}

function clearScene() {
  const previousStyle = document.getElementById("player-code");
  if (previousStyle) {
    previousStyle.remove();
  }
  sceneRoot.innerHTML = "";
}

function applyCode(injectionMode, code) {
  if (injectionMode === "styleAppend") {
    const style = document.createElement("style");
    style.id = "player-code";
    style.textContent = code;
    document.head.appendChild(style);
    return { error: null, playerSelection: null };
  }

  if (injectionMode === "selector") {
    try {
      return { error: null, playerSelection: sceneRoot.querySelectorAll(code) };
    } catch (e) {
      return { error: String(e && e.message ? e.message : e), playerSelection: null };
    }
  }

  if (injectionMode === "script") {
    try {
      const fn = new Function(code);
      fn();
      return { error: null, playerSelection: null };
    } catch (e) {
      return { error: String(e && e.message ? e.message : e), playerSelection: null };
    }
  }

  return { error: `Неизвестный injectionMode: ${injectionMode}`, playerSelection: null };
}

function matchesForbiddenPattern(code, patterns) {
  for (const pattern of patterns || []) {
    try {
      const regex = new RegExp(pattern);
      if (regex.test(code)) {
        return pattern;
      }
    } catch {
      // Некорректное регулярное выражение в конфигурации уровня — пропускаем.
    }
  }
  return null;
}

// Раннер не знает заранее размер сцены конкретного уровня (он задаётся в
// levels.json и отличается от уровня к уровню) — родитель измеряет его здесь
// и подгоняет размер sandbox-iframe под него (sandbox.js), вместо того чтобы
// держать один фиксированный размер для всех уровней.
// Меряем именно #pond, а не document.documentElement.scrollWidth/Height: html —
// обычный block-элемент, его width:auto растягивается на всю ширину текущего
// iframe (а не сжимается до контента), так что scrollWidth в момент, когда
// #pond уже уже, чем предыдущий/запасной размер iframe, просто вернул бы этот
// предыдущий размер, а не настоящую ширину сцены. #pond у каждого уровня —
// либо с явным width (flex-уровни и все grid-уровни после правки размера
// окна), либо сам задаёт свой размер через grid-template — его собственный
// getBoundingClientRect() не зависит от текущего размера iframe.
function measureSceneSize() {
  const pond = document.getElementById("pond");
  if (pond) {
    const r = pond.getBoundingClientRect();
    // Фон сцены измеряется и отдаётся родителю вместе с размером — если
    // размер определён на долю пикселя неточно (округление/sub-pixel layout)
    // и всё же остаётся край, он того же цвета, что и сама сцена, а не
    // произвольного цвета iframe по умолчанию.
    const background = getComputedStyle(pond).backgroundColor;
    return { width: Math.ceil(r.width), height: Math.ceil(r.height), background };
  }
  return {
    width: document.documentElement.scrollWidth,
    height: document.documentElement.scrollHeight,
    background: null,
  };
}

async function runChecks(msg) {
  const code = msg.code || "";

  clearScene();
  baseCssEl.textContent = msg.scene.baseCss || "";
  sceneRoot.innerHTML = msg.scene.html || "";

  const forbiddenHit = matchesForbiddenPattern(code, msg.forbiddenPatterns);
  if (forbiddenHit) {
    return {
      passed: false,
      checks: [],
      error: `Код содержит запрещённую конструкцию: ${forbiddenHit}`,
      size: measureSceneSize(),
    };
  }

  const { error: applyError, playerSelection } = applyCode(msg.injectionMode, code);

  await waitTwoFrames();

  const size = measureSceneSize();

  if (applyError) {
    return { passed: false, checks: [], error: applyError, size };
  }

  const checks = (msg.validation && msg.validation.checks) || [];
  const ctx = {
    sceneRoot,
    tolerancePx: (msg.validation && msg.validation.tolerancePx) ?? 8,
    playerSelection,
  };

  const results = [];
  for (const check of checks) {
    const fn = CHECKS[check.kind];
    if (!fn) {
      return {
        passed: false,
        checks: results,
        error: `Неизвестный вид проверки: ${check.kind}`,
        size,
      };
    }
    results.push(fn(check, ctx));
  }

  const passed = results.every((r) => r.passed);
  return { passed, checks: results, error: null, size };
}

window.addEventListener("message", (event) => {
  if (event.source !== window.parent) {
    return;
  }

  const msg = event.data;
  if (!msg || msg.type !== "RUN") {
    return;
  }

  runChecks(msg)
    .then((result) => {
      window.parent.postMessage(
        {
          type: "RUN_RESULT",
          runId: msg.runId,
          passed: result.passed,
          checks: result.checks,
          error: result.error,
          size: result.size,
        },
        "*"
      );
    })
    .catch((e) => {
      window.parent.postMessage(
        {
          type: "RUN_RESULT",
          runId: msg.runId,
          passed: false,
          checks: [],
          error: String(e && e.message ? e.message : e),
        },
        "*"
      );
    });
});

window.parent.postMessage({ type: "RUNNER_READY" }, "*");
