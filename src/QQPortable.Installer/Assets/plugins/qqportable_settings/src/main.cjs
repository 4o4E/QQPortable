"use strict";

const { dialog, ipcMain } = require("electron");
const { spawn, spawnSync } = require("node:child_process");
const { existsSync, readFileSync, renameSync, unlinkSync, writeFileSync } = require("node:fs");
const path = require("node:path");

const CHANNEL = "qqportable.settings";
const portableRoot = path.dirname(LiteLoader.path.root);
const configPath = path.join(portableRoot, "QQPortable.json");
const launcherPath = path.join(portableRoot, "QQPortable.exe");
const settingsPath = path.join(portableRoot, "QQPortable.setting.exe");

function readConfig() {
  if (!existsSync(configPath)) {
    const defaults = { autoStart: false };
    writeConfig(defaults);
    return defaults;
  }

  let value;
  try {
    value = JSON.parse(readFileSync(configPath, "utf8"));
  } catch {
    throw new Error("QQPortable.json 不是有效的 JSON：" + configPath);
  }
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    throw new Error("QQPortable.json 必须是 JSON 对象：" + configPath);
  }
  if (!Object.prototype.hasOwnProperty.call(value, "autoStart")) {
    value.autoStart = false;
  }
  if (typeof value.autoStart !== "boolean") {
    throw new Error("QQPortable.json 中的 autoStart 必须是 true 或 false");
  }
  return value;
}

function writeConfig(value) {
  const temporary = configPath + "." + process.pid + "." + Date.now() + ".tmp";
  writeFileSync(temporary, JSON.stringify(value, null, 2) + "\n", "utf8");
  try {
    // Windows 上 rename 会替换已存在的目标文件，避免先删除造成配置短暂消失。
    renameSync(temporary, configPath);
  } catch (error) {
    try {
      if (existsSync(temporary)) unlinkSync(temporary);
    } catch {
      // 临时文件清理失败不覆盖原始写入错误。
    }
    throw error;
  }
}

function applyAutoStart() {
  if (!existsSync(settingsPath)) {
    throw new Error("找不到 QQPortable.setting.exe，无法更新开机自启：" + settingsPath);
  }
  const result = spawnSync(settingsPath, ["--apply"], {
    cwd: portableRoot,
    windowsHide: true,
    encoding: "utf8",
    timeout: 15000,
  });
  if (result.error) {
    throw result.error;
  }
  if (result.status !== 0) {
    throw new Error("QQPortable.setting.exe 执行失败，退出码 " + result.status + "。请运行 QQPortable.setting.exe 检查。");
  }
}

function restartQQ() {
  if (!existsSync(launcherPath)) {
    dialog.showErrorBox("QQPortable", "找不到 QQPortable.exe，无法重启 QQ：" + launcherPath);
    return;
  }
  try {
    const child = spawn(launcherPath, ["--restart"], {
      cwd: portableRoot,
      detached: true,
      stdio: "ignore",
      windowsHide: true,
    });
    child.on("error", (error) => {
      dialog.showErrorBox("QQPortable", "无法重启 QQ：" + error.message);
    });
    child.unref();
  } catch (error) {
    dialog.showErrorBox("QQPortable", "无法重启 QQ：" + error.message);
  }
}

function registerIpc() {
  if (globalThis.__qqPortableIpcRegistered) return;
  globalThis.__qqPortableIpcRegistered = true;

  ipcMain.handle(CHANNEL + ".getConfig", () => {
    const config = readConfig();
    return { autoStart: config.autoStart === true, configPath };
  });

  ipcMain.handle(CHANNEL + ".setAutoStart", (_event, enabled) => {
    if (typeof enabled !== "boolean") {
      throw new Error("autoStart 必须是 true 或 false");
    }
    const config = readConfig();
    const previous = config.autoStart === true;
    config.autoStart = enabled;
    writeConfig(config);
    try {
      applyAutoStart();
    } catch (error) {
      config.autoStart = previous;
      try {
        writeConfig(config);
        applyAutoStart();
      } catch {
        // 回滚失败保留原始错误，界面会显示并让用户手动检查。
      }
      throw new Error("无法更新开机自启：" + error.message);
    }
    return { autoStart: enabled, configPath };
  });

  ipcMain.handle(CHANNEL + ".restartQQ", () => {
    restartQQ();
    return true;
  });
}

registerIpc();
