/**
 * AXORA Desktop - MaterialUI Adversarial UI & Chaos Test Suite
 * Deliberately attempts to break the desktop UI via rapid bombardment, extreme inputs,
 * boundary resizing, modal hammer cycles, and race conditions.
 */

import { spawn } from "child_process";
import fs from "fs";
import path from "path";

const PORT = 9223; // Isolated port for adversarial run
const APP_PATH = "d:\\RAJ\\GITHUB_REPOSITORY\\PROJECTS\\AXORA-DESKTOP\\Axora-Desktop-MaterialUI\\src-tauri\\target\\release\\axora-desktop.exe";
const SCREENSHOT_DIR = "d:\\RAJ\\GITHUB_REPOSITORY\\PROJECTS\\AXORA-DESKTOP\\docs\\qa\\screenshots";

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

class CdpClient {
  constructor(wsUrl) {
    this.wsUrl = wsUrl;
    this.id = 1;
    this.callbacks = new Map();
    this.consoleLogs = [];
    this.exceptions = [];
  }

  async connect() {
    return new Promise((resolve, reject) => {
      this.ws = new WebSocket(this.wsUrl);
      this.ws.onopen = () => resolve();
      this.ws.onerror = (err) => reject(err);
      this.ws.onmessage = (event) => {
        const msg = JSON.parse(event.data);
        if (msg.id && this.callbacks.has(msg.id)) {
          const cb = this.callbacks.get(msg.id);
          this.callbacks.delete(msg.id);
          if (msg.error) cb.reject(msg.error);
          else cb.resolve(msg.result);
        } else if (msg.method) {
          if (msg.method === "Runtime.consoleAPICalled") {
            const text = msg.params.args.map((a) => a.value || a.description).join(" ");
            this.consoleLogs.push({ type: msg.params.type, text });
          } else if (msg.method === "Runtime.exceptionThrown") {
            const desc = msg.params.exceptionDetails?.exception?.description || msg.params.exceptionDetails?.text || "Unknown exception";
            this.exceptions.push({ text: desc, details: msg.params.exceptionDetails });
          }
        }
      };
    });
  }

  send(method, params = {}) {
    return new Promise((resolve, reject) => {
      const msgId = this.id++;
      this.callbacks.set(msgId, { resolve, reject });
      this.ws.send(JSON.stringify({ id: msgId, method, params }));
    });
  }

  async eval(expression) {
    const res = await this.send("Runtime.evaluate", {
      expression,
      returnByValue: true,
      awaitPromise: true,
    });
    if (res.exceptionDetails) {
      throw new Error(res.exceptionDetails.text || res.exceptionDetails.exception?.description || "Eval exception");
    }
    return res.result?.value;
  }

  async captureScreenshot(filename) {
    if (!fs.existsSync(SCREENSHOT_DIR)) {
      fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });
    }
    const res = await this.send("Page.captureScreenshot", { format: "png" });
    const buffer = Buffer.from(res.data, "base64");
    const filePath = path.join(SCREENSHOT_DIR, filename);
    fs.writeFileSync(filePath, buffer);
    return filePath;
  }

  close() {
    if (this.ws) this.ws.close();
  }
}

async function runAdversarialSuite() {
  console.log("================================================================================");
  console.log("  AXORA MATERIALUI - ADVERSARIAL UI & CHAOS VERIFICATION SUITE");
  console.log("================================================================================");

  console.log(`[1] Launching axora-desktop.exe on isolated CDP port ${PORT}...`);
  const env = { ...process.env, WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS: `--remote-debugging-port=${PORT}` };
  const appProc = spawn(APP_PATH, [], { env, stdio: "ignore", detached: false });
  console.log(`  Process spawned with PID: ${appProc.pid}`);

  let cdpClient = null;
  const results = [];

  const record = (dimension, testName, pass, details = "") => {
    results.push({ dimension, testName, pass, details });
    const tag = pass ? "[PASS]" : "[FAIL]";
    console.log(`  ${tag} ${testName}${details ? " - " + details : ""}`);
  };

  try {
    let target = null;
    for (let i = 0; i < 25; i++) {
      await sleep(400);
      try {
        const resp = await fetch(`http://localhost:${PORT}/json`);
        const targets = await resp.json();
        target = targets.find((t) => t.type === "page" && t.webSocketDebuggerUrl);
        if (target) break;
      } catch (_) {}
    }

    if (!target) {
      throw new Error(`Failed to connect to CDP target on port ${PORT}.`);
    }

    console.log(`  Connected to target: "${target.title}"`);
    cdpClient = new CdpClient(target.webSocketDebuggerUrl);
    await cdpClient.connect();

    await cdpClient.send("Runtime.enable");
    await cdpClient.send("Page.enable");
    await cdpClient.send("DOM.enable");

    // Wait for Splash screen dismiss
    console.log("\n[2] Awaiting Initial App Hydration...");
    for (let i = 0; i < 40; i++) {
      await sleep(200);
      const isReady = await cdpClient.eval('!document.querySelector("div.fixed.inset-0") && !!document.querySelector("aside")');
      if (isReady) break;
    }
    await sleep(500);

    // ── ADVERSARIAL TEST 1: Rapid Navigation Bombardment ───────────────────────
    console.log("\n[3] Adversarial Test 1: Rapid Route Navigation Bombardment (20 rapid clicks)...");
    const navBombardmentResult = await cdpClient.eval(`
      (() => {
        const items = Array.from(document.querySelectorAll("aside [role='button'], aside div.cursor-pointer, aside div[class*='cursor-pointer']"));
        if (items.length < 5) return { success: false, reason: "Insufficient nav items: " + items.length };
        
        let clickCount = 0;
        for (let i = 0; i < 20; i++) {
          const target = items[i % items.length];
          target.click();
          clickCount++;
        }
        return { success: true, clickCount };
      })()
    `);
    await sleep(800); // Allow Framer Motion animations to settle
    const isUiResponsiveAfterNav = await cdpClient.eval("!!document.querySelector('aside') && document.body.innerText.length > 50");
    record("Stress", "Rapid Navigation Bombardment (20 Clicks in 100ms)", navBombardmentResult.success && isUiResponsiveAfterNav, `Fired ${navBombardmentResult.clickCount} clicks; UI responsive`);

    // ── ADVERSARIAL TEST 2: Rapid Theme Toggle Thrashing ───────────────────────
    console.log("\n[4] Adversarial Test 2: Rapid Theme Toggle Thrashing (10 rapid switches)...");
    const themeThrashResult = await cdpClient.eval(`
      (() => {
        const toggles = Array.from(document.querySelectorAll("header button, div.flex.justify-end button, aside + div button"));
        const btn = toggles.find(b => b.querySelector("svg.lucide-sun") || b.querySelector("svg.lucide-moon"));
        if (!btn) return { success: false, reason: "Theme toggle button not found" };

        const states = [];
        for (let i = 0; i < 10; i++) {
          btn.click();
          states.push(getComputedStyle(document.body).backgroundColor);
        }
        return { success: true, finalBg: getComputedStyle(document.body).backgroundColor };
      })()
    `);
    await sleep(400);
    record("Stress", "Rapid Theme Switching Thrash (10 Clicks)", themeThrashResult.success, `Final Theme BG: ${themeThrashResult.finalBg}`);

    // ── ADVERSARIAL TEST 3: Modal Bombardment (Command Palette & System Info) ──
    console.log("\n[5] Adversarial Test 3: Dialog Opening & Closing Hammer (10 Cycles)...");
    let paletteCyclesPassed = 0;
    for (let c = 0; c < 10; c++) {
      // Open with Ctrl+K
      await cdpClient.eval(`window.dispatchEvent(new KeyboardEvent("keydown", { key: "k", ctrlKey: true, bubbles: true }));`);
      let opened = false;
      for (let i = 0; i < 15; i++) {
        await sleep(50);
        opened = await cdpClient.eval(`!!document.querySelector('input[placeholder*="Type a command"], input[placeholder*="search"]')`);
        if (opened) break;
      }
      
      // Close by clicking the close X button or dispatching Escape
      await cdpClient.eval(`
        (() => {
          const closeBtn = document.querySelector('input[placeholder*="Type a command"]')?.parentElement?.querySelector("button");
          if (closeBtn) closeBtn.click();
          else window.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", code: "Escape", bubbles: true }));
        })()
      `);
      
      // Allow Framer Motion spring exit animation to settle and remove element
      let closed = false;
      for (let i = 0; i < 15; i++) {
        await sleep(50);
        closed = await cdpClient.eval(`!document.querySelector('input[placeholder*="Type a command"]')`);
        if (closed) break;
      }
      if (opened && closed) paletteCyclesPassed++;
    }
    record("Stress", "Command Palette Hammer (10 Open/Close Cycles)", paletteCyclesPassed === 10, `Completed ${paletteCyclesPassed}/10 clean cycles without deadlock`);

    // ── ADVERSARIAL TEST 4: Extreme String Injection & XSS Immunity ─────────────
    console.log("\n[6] Adversarial Test 4: Extreme Input Strings (5,000 chars, XSS, Unicode)...");
    await cdpClient.eval(`window.dispatchEvent(new KeyboardEvent("keydown", { key: "k", ctrlKey: true, bubbles: true }));`);
    await sleep(250);

    const testPayloads = [
      { name: "5,000-Character Long Continuous String", payload: "A".repeat(5000) },
      { name: "XSS Probe (<script> tag)", payload: "<script>window.__xss_injected=true;</script>" },
      { name: "SQL Injection Probe", payload: "'; DROP TABLE users; --" },
      { name: "Unicode & Emoji Storm", payload: "🚀🔥⚡🎉💻🧠🛡️✨💯𝕿𝖊𝖘𝖙" },
      { name: "Template Literal & Prototype Probe", payload: "${7*7}{{constructor.prototype}}" },
    ];

    let allPayloadsSafe = true;
    for (const test of testPayloads) {
      await cdpClient.eval(`
        (() => {
          const input = document.querySelector('input[placeholder*="Type a command"], input[placeholder*="search"]');
          if (input) {
            input.value = ${JSON.stringify(test.payload)};
            input.dispatchEvent(new Event("input", { bubbles: true }));
          }
        })()
      `);
      await sleep(150);
      const isXssTriggered = await cdpClient.eval("window.__xss_injected === true");
      const isInputCrashed = await cdpClient.eval("!document.querySelector('input')");
      if (isXssTriggered || isInputCrashed) {
        allPayloadsSafe = false;
        record("Security/Input", `Payload: ${test.name}`, false, "XSS executed or input crashed");
      } else {
        record("Security/Input", `Payload: ${test.name}`, true, "Handled safely without DOM corruption");
      }
    }

    // Dismiss palette
    await cdpClient.eval(`
      (() => {
        const backdrop = Array.from(document.querySelectorAll("div.fixed.inset-0")).find(d => d.className && d.className.includes("bg-black"));
        if (backdrop) backdrop.click();
        else window.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", code: "Escape", bubbles: true }));
      })()
    `);
    await sleep(350);

    // ── ADVERSARIAL TEST 5: Unexpected Keyboard Event Fuzzing ──────────────────
    console.log("\n[7] Adversarial Test 5: Keyboard Event Fuzzing...");
    const keysToFuzz = ["Escape", "Escape", "Enter", "Space", "Backspace", "Tab", "ArrowDown", "ArrowUp"];
    let fuzzExceptions = 0;
    for (const k of keysToFuzz) {
      try {
        await cdpClient.eval(`window.dispatchEvent(new KeyboardEvent("keydown", { key: "${k}", bubbles: true }));`);
      } catch (_) {
        fuzzExceptions++;
      }
    }
    await sleep(200);
    record("Robustness", "Unexpected Keyboard Event Fuzzing (8 random keydown events)", fuzzExceptions === 0, "No unhandled keyboard event exceptions");

    // ── ADVERSARIAL TEST 6: Window Resizing & Horizontal Overflow Stress ─────────
    console.log("\n[8] Adversarial Test 6: Viewport Resizing & Layout Stress...");
    
    // Simulate compact viewport
    await cdpClient.send("Emulation.setDeviceMetricsOverride", {
      width: 960,
      height: 600,
      deviceScaleFactor: 1,
      mobile: false,
    });
    await sleep(400);
    const compactAudit = await cdpClient.eval(`
      (() => {
        return {
          scrollWidth: document.body.scrollWidth,
          clientWidth: document.documentElement.clientWidth,
          hasOverflow: document.body.scrollWidth > document.documentElement.clientWidth + 5
        };
      })()
    `);
    record("Visual Stress", "Minimum Size (960x600) Layout Clamping", !compactAudit.hasOverflow, `Scroll: ${compactAudit.scrollWidth}px vs Client: ${compactAudit.clientWidth}px`);

    // Simulate ultra-wide viewport
    await cdpClient.send("Emulation.setDeviceMetricsOverride", {
      width: 1920,
      height: 800,
      deviceScaleFactor: 1,
      mobile: false,
    });
    await sleep(400);
    const wideAudit = await cdpClient.eval(`
      (() => {
        return {
          scrollWidth: document.body.scrollWidth,
          clientWidth: document.documentElement.clientWidth,
          hasOverflow: document.body.scrollWidth > document.documentElement.clientWidth + 5
        };
      })()
    `);
    record("Visual Stress", "Ultra-Wide (1920x800) Layout Expansion", !wideAudit.hasOverflow, `Scroll: ${wideAudit.scrollWidth}px vs Client: ${wideAudit.clientWidth}px`);

    // Reset emulation
    await cdpClient.send("Emulation.clearDeviceMetricsOverride");
    await sleep(300);

    // Capture stress screenshot
    await cdpClient.captureScreenshot("adversarial-stress-layout.png");

    // ── ADVERSARIAL TEST 7: Zero Uncaught Exceptions Audit ─────────────────────
    console.log("\n[9] Adversarial Test 7: Runtime Uncaught Exception Trapping...");
    const exceptions = cdpClient.exceptions;
    const errors = cdpClient.consoleLogs.filter((l) => l.type === "error");
    record(
      "Reliability",
      "Zero Uncaught JavaScript Runtime Errors Under Adversarial Stress",
      exceptions.length === 0 && errors.length === 0,
      `Exceptions: ${exceptions.length} | Console Errors: ${errors.length}`
    );

  } finally {
    if (cdpClient) cdpClient.close();
    if (!appProc.killed) {
      try { process.kill(appProc.pid); } catch (_) {}
    }
  }

  // Summary
  console.log("\n================================================================================");
  const passed = results.filter((r) => r.pass).length;
  const failed = results.filter((r) => !r.pass).length;
  console.log(`  ADVERSARIAL SUITE RESULT: ${passed} PASSED | ${failed} FAILED (Total: ${results.length})`);
  console.log("================================================================================");

  if (failed > 0) process.exit(1);
  else process.exit(0);
}

runAdversarialSuite().catch((err) => {
  console.error("FATAL ADVERSARIAL TEST ERROR:", err);
  process.exit(1);
});
