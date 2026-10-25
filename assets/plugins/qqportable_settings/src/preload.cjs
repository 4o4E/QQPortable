"use strict";

const { contextBridge, ipcRenderer } = require("electron");

const CHANNEL = "qqportable.settings";

contextBridge.exposeInMainWorld(
  "qqPortableSettings",
  Object.freeze({
    getConfig: () => ipcRenderer.invoke(CHANNEL + ".getConfig"),
    setAutoStart: (enabled) => ipcRenderer.invoke(CHANNEL + ".setAutoStart", enabled),
    restartQQ: () => ipcRenderer.invoke(CHANNEL + ".restartQQ"),
  }),
);
