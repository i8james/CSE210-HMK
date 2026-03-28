(function () {
  "use strict";

  const commanderInput = document.getElementById("commander");
  const decklistInput = document.getElementById("decklist");
  const simCountInput = document.getElementById("sim-count");
  const turnCapInput = document.getElementById("turn-cap");
  const archetypeInput = document.getElementById("archetype");
  const validateQueueBtn = document.getElementById("validate-queue-btn");
  const exportInputBtn = document.getElementById("export-input-btn");
  const runResult = document.getElementById("run-result");

  if (!validateQueueBtn || !exportInputBtn || !runResult) {
    return;
  }

  function normalizeName(name) {
    return (name || "").trim().replace(/\s+/g, " ");
  }

  function toTitleCase(value) {
    return value.replace(/\w\S*/g, function (word) {
      return word.charAt(0).toUpperCase() + word.substring(1).toLowerCase();
    });
  }

  function isBasicLand(name) {
    const n = normalizeName(name).toLowerCase();
    return n === "plains" || n === "island" || n === "swamp" || n === "mountain" || n === "forest" || n === "wastes";
  }

  function parseDecklist(text) {
    const entries = [];
    const lines = (text || "").split(/\r?\n/);

    for (const rawLine of lines) {
      const line = rawLine.trim();
      if (!line || line.startsWith("#") || line.startsWith("//")) {
        continue;
      }

      let quantity = 1;
      let name = line;

      const leading = line.match(/^(\d+)\s*x?\s+(.+)$/i);
      if (leading) {
        quantity = Math.max(1, parseInt(leading[1], 10));
        name = leading[2];
      } else {
        const trailing = line.match(/^(.+?)\s+[xX](\d+)$/);
        if (trailing) {
          quantity = Math.max(1, parseInt(trailing[2], 10));
          name = trailing[1];
        }
      }

      const normalized = normalizeName(name);
      if (!normalized) {
        continue;
      }

      entries.push({ name: normalized, quantity: quantity });
    }

    return entries;
  }

  function validateInput() {
    const errors = [];
    const commander = normalizeName(commanderInput ? commanderInput.value : "");
    const entries = parseDecklist(decklistInput ? decklistInput.value : "");
    const totalCards = entries.reduce(function (sum, item) { return sum + item.quantity; }, 0);

    if (!commander) {
      errors.push("Commander is required.");
    }

    if (totalCards !== 99) {
      errors.push("Decklist must contain exactly 99 cards (currently " + totalCards + ").");
    }

    const counts = new Map();
    for (const entry of entries) {
      const key = entry.name.toLowerCase();
      counts.set(key, (counts.get(key) || 0) + entry.quantity);
    }

    for (const pair of counts.entries()) {
      const key = pair[0];
      const qty = pair[1];
      if (qty <= 1) {
        continue;
      }

      const display = toTitleCase(key);
      if (!isBasicLand(display)) {
        errors.push(display + " appears " + qty + " times. Commander is singleton (except basic lands and special exceptions).");
      }
    }

    return {
      isValid: errors.length === 0,
      errors: errors,
      commander: commander,
      entries: entries,
      totalCards: totalCards
    };
  }

  function buildQueuePayload(validated) {
    return {
      source: "fizban-web-portal",
      queuedAtUtc: new Date().toISOString(),
      commander: validated.commander,
      simulations: simCountInput ? simCountInput.value : "50k",
      turnCap: turnCapInput ? turnCapInput.value : "10",
      archetype: archetypeInput ? archetypeInput.value : "Auto Detect",
      decklistText: decklistInput ? decklistInput.value : "",
      cardCount: validated.totalCards
    };
  }

  function downloadTextFile(filename, content, mimeType) {
    const blob = new Blob([content], { type: mimeType });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
  }

  function setResult(content, isError) {
    runResult.textContent = content;
    runResult.classList.remove("result-success", "result-error");
    runResult.classList.add(isError ? "result-error" : "result-success");
  }

  validateQueueBtn.addEventListener("click", function () {
    const validated = validateInput();
    if (!validated.isValid) {
      setResult("Validation failed:\n- " + validated.errors.join("\n- "), true);
      return;
    }

    const payload = buildQueuePayload(validated);
    const payloadJson = JSON.stringify(payload, null, 2);

    try {
      localStorage.setItem("fizbanLastQueue", payloadJson);
    } catch (error) {
      // Ignore storage failures in restricted contexts.
    }

    const stamp = new Date().toISOString().replace(/[:T]/g, "-").slice(0, 16);
    downloadTextFile("fizban-queue-" + stamp + ".json", payloadJson, "application/json");

    setResult(
      "Validation passed.\n\nQueued analysis payload exported as JSON.\n" +
      "Card count: " + validated.totalCards + "\n" +
      "Commander: " + validated.commander + "\n" +
      "Simulations: " + payload.simulations + "\n" +
      "Turn cap: " + payload.turnCap + "\n" +
      "Archetype: " + payload.archetype,
      false
    );
  });

  exportInputBtn.addEventListener("click", function () {
    const commander = normalizeName(commanderInput ? commanderInput.value : "");
    const decklist = decklistInput ? decklistInput.value.trim() : "";
    const output = "Commander: " + commander + "\n\n" + decklist + "\n";
    const stamp = new Date().toISOString().replace(/[:T]/g, "-").slice(0, 16);
    downloadTextFile("fizban-input-" + stamp + ".txt", output, "text/plain");

    setResult("Input exported as .txt for desktop import.\nCommander: " + (commander || "(not set)"), false);
  });
})();
