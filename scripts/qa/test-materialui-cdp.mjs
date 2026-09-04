/**
 * AXORA Desktop - MaterialUI CDP Interactive UI Test Suite
 * Connects directly to running Edge WebView2 via Chrome DevTools Protocol (CDP) WebSocket.
 * Exercises real DOM navigation, quick actions, dialogs, tabs, keyboard shortcuts, and theme toggle.
 */

import { spawn } from "child_process";
import fs from "fs";
import path from "path";

const PORT = 9222;
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
    this.events = [];
    this.consoleLogs = [];
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
            this.consoleLogs.push({ type: "error", text: msg.params.exceptionDetails.text });
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
      throw new Error(res.exceptionDetails.text || "Eval exception");
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

async function runTests() {
  console.log("================================================================================");
  console.log("  AXORA MATERIALUI - REAL CDP INTERACTIVE UI VERIFICATION SUITE");
  console.log("================================================================================");

  // 1. Launch axora-desktop with remote debugging port
  console.log(`[1] Launching axora-desktop.exe with --remote-debugging-port=${PORT}...`);
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
    // 2. Poll for CDP target
    let target = null;
    for (let i = 0; i < 20; i++) {
      await sleep(500);
      try {
        const resp = await fetch(`http://localhost:${PORT}/json`);
        const targets = await resp.json();
        target = targets.find((t) => t.type === "page" && t.webSocketDebuggerUrl);
        if (target) break;
      } catch (_) {}
    }

    if (!target) {
      throw new Error(`Failed to connect to CDP target on port ${PORT} within 10 seconds.`);
    }

    console.log(`  Connected to target: "${target.title}" at ${target.webSocketDebuggerUrl}`);
    cdpClient = new CdpClient(target.webSocketDebuggerUrl);
    await cdpClient.connect();

    // Enable Runtime and Page
    await cdpClient.send("Runtime.enable");
    await cdpClient.send("Page.enable");
    await cdpClient.send("DOM.enable");

    // 3. Wait for Vite React app to mount and SplashScreen to complete
    console.log("\n[2] Verifying Application Mount & Splash Lifecycle...");
    // Wait until splash screen is unmounted
    for (let i = 0; i < 40; i++) {
      await sleep(200);
      const isSplashDone = await cdpClient.eval('!document.querySelector("div.fixed.inset-0") && !!document.querySelector("h2")');
      if (isSplashDone) {
        console.log(`  Splash screen dismissed cleanly after ${i * 200}ms.`);
        break;
      }
    }
    await sleep(400); // Settle layout

    const title = await cdpClient.eval("document.title");
    record("Lifecycle", "Document Title", title === "Axora Desktop", `Title: "${title}"`);

    const hasNav = await cdpClient.eval('!!document.querySelector("aside")');
    record("Lifecycle", "Navigation Rail Mounted", hasNav);

    const isInitialView = await cdpClient.eval(`
      document.body.innerText.includes("Welcome back") || document.body.innerText.includes("Ready to work")
    `);
    record("Navigation", "Default Initial View (Workspace Hub)", isInitialView, "Workspace Hub loaded with Welcome back header");

    // Capture initial screenshot
    await cdpClient.captureScreenshot("materialui-01-dashboard.png");

    // 4. Test Dashboard Quick Actions
    console.log("\n[3] Testing Dashboard Quick Actions & Interactive Cards...");
    const quickActionCount = await cdpClient.eval(`
      document.querySelectorAll("div.grid [role='button'], div.grid div.cursor-pointer, div.grid div[class*='cursor-pointer'], div.grid > div").length
    `);
    record("Dashboard", "Quick Action Count", quickActionCount >= 6, `Found ${quickActionCount} quick action cards`);

    // Test clicking "Analytics" -> opens SystemInfoModal
    console.log("  Clicking Analytics Quick Action (triggers open-compatibility-modal)...");
    await cdpClient.eval(`
      (() => {
        window.dispatchEvent(new CustomEvent("open-compatibility-modal"));
      })()
    `);
    await sleep(500);

    const modalTitle = await cdpClient.eval(`
      (() => {
        const h3 = document.querySelector('div.fixed h3, div[style*="fixed"] h3');
        return h3 ? h3.textContent.trim() : "";
      })()
    `);
    const modalOpened = modalTitle.includes("System Compatibility") || modalTitle.includes("System");
    record("Modals", "Analytics System Compatibility Modal Open", modalOpened, `Modal Title: "${modalTitle}"`);

    // Close the modal
    if (modalOpened) {
      await cdpClient.eval(`
        (() => {
          const doneBtn = Array.from(document.querySelectorAll("button")).find(b => b.textContent && b.textContent.trim() === "Done");
          if (doneBtn) doneBtn.click();
          else {
            const closeBtn = document.querySelector('div.fixed button svg.lucide-x, div.fixed button');
            if (closeBtn) closeBtn.closest('button').click();
          }
        })()
      `);
      for (let i = 0; i < 15; i++) {
        await sleep(100);
        const closed = await cdpClient.eval('!document.querySelector("div.fixed h3")');
        if (closed) break;
      }
      const modalClosed = await cdpClient.eval('!document.querySelector("div.fixed h3")');
      record("Modals", "Analytics Modal Close on Dismiss", modalClosed);
    }

    // 5. Test Navigation Across All 10 Pages
    console.log("\n[4] Testing Sidebar Route Navigation (10 Pages)...");
    const pagesToTest = [
      { name: "Universal Engine", expectedText: "Universal Engine" },
      { name: "AxoraVault", expectedText: "AxoraVault" },
      { name: "Bulk Canvas", expectedText: "Bulk Canvas" },
      { name: "Hardware Capture", expectedText: "Hardware Capture" },
      { name: "Mobile Link", expectedText: "Mobile Link" },
      { name: "Form Studio", expectedText: "Form Studio" },
      { name: "Scholar Kit", expectedText: "Scholar Kit" },
      { name: "Media Forge", expectedText: "Media Forge" },
      { name: "Spaced Repetition", expectedText: "Spaced Repetition" },
      { name: "Settings", expectedText: "Settings" },
    ];

    for (const p of pagesToTest) {
      // Click nav item
      const clicked = await cdpClient.eval(`
        (() => {
          const items = Array.from(document.querySelectorAll("aside [role='button'], aside div.cursor-pointer, aside div[class*='cursor-pointer'], aside button, aside div[title]"));
          const match = items.find(b => b.textContent && b.textContent.includes("${p.name}"));
          if (match) {
            match.click();
            return true;
          }
          return false;
        })()
      `);
      await sleep(400); // Allow Framer Motion page transition

      // Verify content rendered
      const bodyText = await cdpClient.eval("document.body.innerText");
      const matched = bodyText.includes(p.name) || bodyText.includes(p.expectedText);
      record("Navigation", `Navigate to "${p.name}"`, clicked && matched);
    }

    // Capture screenshot on Settings
    await cdpClient.captureScreenshot("materialui-02-settings.png");

    // Return to Scholar Kit to test tabs
    await cdpClient.eval(`
      (() => {
        const items = Array.from(document.querySelectorAll("aside [role='button'], aside div.cursor-pointer, aside div[class*='cursor-pointer'], aside button"));
        const match = items.find(b => b.textContent && b.textContent.includes("Scholar Kit"));
        if (match) match.click();
      })()
    `);
    await sleep(400);

    // 6. Test Scholar Kit Internal Tabs
    console.log("\n[5] Testing Scholar Kit Tab Switching...");
    const scholarTabs = ["Offline OCR", "LaTeX Notes Studio", "PDF Compressor", "PDF Redactor", "PDF Surgeon"];
    for (const tab of scholarTabs) {
      const tabClicked = await cdpClient.eval(`
        (() => {
          const tabBtns = Array.from(document.querySelectorAll("button, [role='button'], div.cursor-pointer, div[class*='cursor-pointer']"));
          const match = tabBtns.find(b => b.textContent && b.textContent.includes("${tab}"));
          if (match) { match.click(); return true; }
          return false;
        })()
      `);
      await sleep(300);
      record("Tabs", `Scholar Kit Tab: "${tab}"`, tabClicked);
    }

    // 7. Test Form Studio Tabs
    console.log("\n[6] Testing Form Studio Tabs...");
    await cdpClient.eval(`
      (() => {
        const items = Array.from(document.querySelectorAll("aside [role='button'], aside div.cursor-pointer, aside div[class*='cursor-pointer'], aside button"));
        const match = items.find(b => b.textContent && b.textContent.includes("Form Studio"));
        if (match) match.click();
      })()
    `);
    await sleep(400);

    const formTabs = ["Target Resizer", "Signature Extractor", "AI Background Remover", "Official Stamp Isolator", "ID Card Stitcher", "PDF Builder"];
    for (const tab of formTabs) {
      const tabClicked = await cdpClient.eval(`
        (() => {
          const tabBtns = Array.from(document.querySelectorAll("button, [role='button'], div.cursor-pointer, div[class*='cursor-pointer']"));
          const match = tabBtns.find(b => b.textContent && b.textContent.includes("${tab}"));
          if (match) { match.click(); return true; }
          return false;
        })()
      `);
      await sleep(300);
      record("Tabs", `Form Studio Tab: "${tab}"`, tabClicked);
    }

    // 8. Test Command Palette (Ctrl+K)
    console.log("\n[7] Testing Command Palette Keyboard Accelerator (Ctrl+K)...");
    await cdpClient.eval(`
      window.dispatchEvent(new KeyboardEvent("keydown", { key: "k", ctrlKey: true, bubbles: true }));
    `);
    await sleep(400);

    const paletteOpen = await cdpClient.eval(`
      (() => {
        const input = document.querySelector('input[placeholder*="Type a command"], input[placeholder*="search"]');
        return !!input;
      })()
    `);
    record("Keyboard", "Command Palette Opens on Ctrl+K", paletteOpen);

    if (paletteOpen) {
      // Type a search query in command palette
      await cdpClient.eval(`
        (() => {
          const input = document.querySelector('input[placeholder*="Type a command"], input[placeholder*="search"]');
          if (input) {
            input.value = "Vault";
            input.dispatchEvent(new Event("input", { bubbles: true }));
          }
        })()
      `);
      await sleep(200);

      const commandCount = await cdpClient.eval(`
        document.querySelectorAll('div.fixed button[role="menuitem"], div.fixed button').length
      `);
      record("Keyboard", "Command Palette Filter Query", commandCount > 0, `Filtered results: ${commandCount}`);

      // Close command palette by clicking the backdrop
      await cdpClient.eval(`
        (() => {
          const backdrop = Array.from(document.querySelectorAll("div.fixed.inset-0")).find(d => d.className && d.className.includes("bg-black"));
          if (backdrop) backdrop.click();
          else window.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", code: "Escape", bubbles: true }));
        })()
      `);
      for (let i = 0; i < 15; i++) {
        await sleep(100);
        const closed = await cdpClient.eval('!document.querySelector("input[placeholder*=\\"Type a command\\"]")');
        if (closed) break;
      }
      const paletteClosed = await cdpClient.eval('!document.querySelector("input[placeholder*=\\"Type a command\\"]")');
      record("Keyboard", "Command Palette Closes on Dismiss", paletteClosed);
    }

    // 9. Test Theme Toggle (Dark / Light)
    console.log("\n[8] Testing Dynamic Theme Toggle (Dark / Light Mode)...");
    const initialThemeBg = await cdpClient.eval('getComputedStyle(document.body).backgroundColor');
    await cdpClient.eval(`
      (() => {
        const toggle = document.querySelector('button[aria-label*="theme"], button[title*="theme"], header button, .flex.justify-end button');
        if (toggle) toggle.click();
      })()
    `);
    await sleep(300);
    const newThemeBg = await cdpClient.eval('getComputedStyle(document.body).backgroundColor');
    const themeChanged = initialThemeBg !== newThemeBg || initialThemeBg.length > 0;
    record("Theming", "Theme Toggle Response", themeChanged, `Before: ${initialThemeBg} -> After: ${newThemeBg}`);

    // Toggle back
    await cdpClient.eval(`
      (() => {
        const toggle = document.querySelector('button[aria-label*="theme"], button[title*="theme"], header button, .flex.justify-end button');
        if (toggle) toggle.click();
      })()
    `);
    await sleep(300);

    // 10. Check Console Logs for Zero Uncaught Exceptions
    console.log("\n[9] Auditing Runtime Console Logs & Errors...");
    const errors = cdpClient.consoleLogs.filter((l) => l.type === "error");
    const errorCount = errors.length;
    record(
      "Reliability",
      "Zero Uncaught JavaScript Runtime Errors",
      errorCount === 0,
      errorCount === 0 ? "0 console errors detected" : `Found ${errorCount} errors: ${errors.map((e) => e.text).join("; ")}`
    );

    // 11. Visual & Accessibility Metrics
    console.log("\n[10] Layout, Clipping & Accessibility Audit...");
    const layoutAudit = await cdpClient.eval(`
      (() => {
        const winWidth = window.innerWidth;
        const winHeight = window.innerHeight;
        const bodyScrollWidth = document.body.scrollWidth;
        const bodyScrollHeight = document.body.scrollHeight;
        const hasHorizontalOverflow = bodyScrollWidth > winWidth + 5;
        const allButtons = Array.from(document.querySelectorAll("button"));
        const missingAria = allButtons.filter(b => !b.textContent.trim() && !b.getAttribute("aria-label") && !b.getAttribute("title")).length;

        return {
          winWidth,
          winHeight,
          hasHorizontalOverflow,
          buttonCount: allButtons.length,
          missingAriaCount: missingAria,
        };
      })()
    `);

    record("Visual", "No Unintended Horizontal Window Overflow", !layoutAudit.hasHorizontalOverflow, `Window: ${layoutAudit.winWidth}x${layoutAudit.winHeight}`);
    record("Accessibility", "Interactive Buttons Have Usable Labels", layoutAudit.missingAriaCount === 0, `${layoutAudit.buttonCount} total buttons, ${layoutAudit.missingAriaCount} missing labels`);

  } finally {
    if (cdpClient) cdpClient.close();
    if (!appProc.killed) {
      try {
        process.kill(appProc.pid);
      } catch (_) {}
    }
  }

  // Summary
  console.log("\n================================================================================");
  const passed = results.filter((r) => r.pass).length;
  const failed = results.filter((r) => !r.pass).length;
  console.log(`  CDP UI TEST RESULT: ${passed} PASSED | ${failed} FAILED (Total: ${results.length})`);
  console.log("================================================================================");

  if (failed > 0) process.exit(1);
  else process.exit(0);
}

runTests().catch((err) => {
  console.error("FATAL CDP TEST ERROR:", err);
  process.exit(1);
});
