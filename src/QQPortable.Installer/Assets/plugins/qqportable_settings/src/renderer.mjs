const STYLE_ID = "qqportable-settings-style";
const RESTART_LABEL = "重启 QQ";

const STYLE = `
.qqportable-settings { width: 100%; box-sizing: border-box; color: var(--text_primary, currentColor); }
.qqportable-settings setting-section { display: block; margin-bottom: 20px; }
.qqportable-settings setting-panel { overflow: hidden; }
.qqportable-settings .qp-row { min-height: 72px; display: flex; align-items: center; justify-content: space-between; gap: 24px; padding: 14px 16px; box-sizing: border-box; }
.qqportable-settings .qp-copy { min-width: 0; display: grid; gap: 4px; }
.qqportable-settings .qp-title { font-size: 14px; line-height: 20px; color: var(--text_primary, currentColor); }
.qqportable-settings .qp-description { font-size: 12px; line-height: 18px; color: var(--text_secondary, color-mix(in srgb, currentColor 65%, transparent)); }
.qqportable-settings .qp-switch { position: relative; width: 34px; height: 20px; flex: 0 0 auto; }
.qqportable-settings .qp-switch input { position: absolute; inset: 0; margin: 0; opacity: 0; cursor: pointer; }
.qqportable-settings .qp-track { position: absolute; inset: 0; border-radius: 10px; background: color-mix(in srgb, currentColor 22%, transparent); transition: background .18s ease; pointer-events: none; }
.qqportable-settings .qp-track::after { content: ""; position: absolute; width: 14px; height: 14px; left: 3px; top: 3px; border-radius: 50%; background: var(--icon_white, #fff); box-shadow: 0 1px 4px rgb(0 0 0 / 22%); transition: transform .18s ease; }
.qqportable-settings .qp-switch input:checked + .qp-track { background: var(--brand_standard, #0099ff); }
.qqportable-settings .qp-switch input:checked + .qp-track::after { transform: translateX(14px); }
.qqportable-settings .qp-switch input:focus-visible + .qp-track { box-shadow: 0 0 0 2px color-mix(in srgb, var(--brand_standard, #0099ff) 32%, transparent); }
.qqportable-settings .qp-switch input:disabled { cursor: default; }
.qqportable-settings .qp-switch input:disabled + .qp-track { opacity: .45; }
.qqportable-settings .qp-footer { min-height: 24px; padding: 0 16px 12px; box-sizing: border-box; }
.qqportable-settings .qp-message { font-size: 12px; line-height: 18px; color: var(--text_secondary, color-mix(in srgb, currentColor 65%, transparent)); white-space: pre-wrap; }
.qqportable-settings .qp-message[data-kind="success"] { color: var(--success_standard, #2da44e); }
.qqportable-settings .qp-message[data-kind="error"] { color: var(--error_standard, #d1242f); }
.qqportable-settings .qp-path { font-size: 12px; line-height: 18px; color: var(--text_secondary, color-mix(in srgb, currentColor 65%, transparent)); word-break: break-all; padding: 0 2px; }
`;

function injectStyle() {
  if (document.getElementById(STYLE_ID)) return;
  const style = document.createElement("style");
  style.id = STYLE_ID;
  style.textContent = STYLE;
  document.head.appendChild(style);
}

const MORE_SELECTOR = ".func-menu-more-component";
const MENU_SELECTOR = ".q-context-menu.more-menu";

// LiteLoader may miss a component when recording DOM bridges. Walk Vue's actual
// rendered tree instead of assuming the first recorded instance owns the menu.
function findMoreMenuItems(more) {
  const visitedComponents = new Set();
  const visitedVnodes = new Set();
  const pending = [];

  function visitComponent(component) {
    if (!component || component.isUnmounted || visitedComponents.has(component)) return;
    visitedComponents.add(component);
    const items = component.ctx?.menuItem;
    if (
      component.vnode?.el === more &&
      Array.isArray(items) &&
      items.some((item) => item?.label === "设置")
    ) {
      return items;
    }
    if (component.subTree) pending.push(component.subTree);
  }

  function visitTree() {
    while (pending.length) {
      const vnode = pending.pop();
      if (!vnode || visitedVnodes.has(vnode)) continue;
      visitedVnodes.add(vnode);
      if (Array.isArray(vnode)) {
        pending.push(...vnode);
        continue;
      }
      const items = visitComponent(vnode.component);
      if (items) return items;
      if (Array.isArray(vnode.children)) pending.push(...vnode.children);
      if (vnode.suspense?.activeBranch) pending.push(vnode.suspense.activeBranch);
    }
  }

  for (const component of more.__VUE__ ?? []) {
    const items = visitComponent(component);
    if (items) return items;
  }
  for (let element = more; element; element = element.parentElement) {
    for (const component of element.__VUE__ ?? []) {
      const items = visitComponent(component.root ?? component);
      if (items) return items;
    }
  }
  const nearbyItems = visitTree();
  if (nearbyItems) return nearbyItems;

  // A sibling can be the only recorded element of the same Vue root.
  for (const element of document.querySelectorAll(".vue-component")) {
    for (const component of element.__VUE__ ?? []) {
      const items = visitComponent(component.root ?? component);
      if (items) return items;
    }
    const items = visitTree();
    if (items) return items;
  }
}

function ensureMoreMenuRestartItem(items) {
  const settingsIndex = items.findIndex((item) => item?.label === "设置");
  const restartIndex = items.findIndex((item) => item?.label === RESTART_LABEL);
  if (
    restartIndex === settingsIndex + 1 &&
    !items.some((item, index) => index !== restartIndex && item?.label === RESTART_LABEL)
  ) {
    return;
  }
  // QQ can replace or reorder the array. Repair only our item, preserving all
  // native entries and the visibility decisions made by other plugins.
  const restartItem = restartIndex < 0 ? {
    label: RESTART_LABEL,
    icon: "refresh",
    handler: () => window.qqPortableSettings.restartQQ(),
    dtItemType: 0,
    when: true,
  } : items[restartIndex];
  for (let index = items.length - 1; index >= 0; index--) {
    if (items[index]?.label === RESTART_LABEL) items.splice(index, 1);
  }
  items.splice(items.findIndex((item) => item?.label === "设置") + 1, 0, restartItem);
}

function initMoreMenuRestart() {
  let failed = false;
  let frame = null;

  function prepare(more, reportFailure) {
    const items = findMoreMenuItems(more);
    if (!items) {
      if (reportFailure && !failed) {
        console.error("[QQPortable] 无法定位“更多”菜单中包含“设置”的组件数据；组件就绪或再次打开菜单时将重新处理。");
        failed = true;
      }
      return;
    }
    ensureMoreMenuRestartItem(items);
    failed = false;
  }

  function onClick(event) {
    const more = event.target?.closest?.(MORE_SELECTOR);
    if (more) prepare(more, true);
  }

  function prepareCurrent() {
    frame = null;
    const more = document.querySelector(MORE_SELECTOR);
    if (more) prepare(more, !!document.querySelector(MENU_SELECTOR));
  }

  function affectsMenu(node) {
    return node.nodeType === Node.ELEMENT_NODE &&
      (node.matches(`${MORE_SELECTOR}, ${MENU_SELECTOR}`) ||
        !!node.querySelector(`${MORE_SELECTOR}, ${MENU_SELECTOR}`));
  }

  // Observe menu creation/replacement, not message updates. The next frame lets
  // Vue finish mounting before discovery; every click also retries synchronously.
  const observer = new MutationObserver((records) => {
    if (frame !== null) return;
    if (records.some((record) =>
      [...record.addedNodes, ...record.removedNodes].some(affectsMenu))) {
      frame = requestAnimationFrame(prepareCurrent);
    }
  });
  observer.observe(document, { childList: true, subtree: true });
  document.addEventListener("click", onClick, true);
  frame = requestAnimationFrame(prepareCurrent);
  window.addEventListener("pagehide", () => {
    observer.disconnect();
    document.removeEventListener("click", onClick, true);
    if (frame !== null) cancelAnimationFrame(frame);
  }, { once: true });
}

function onSettingWindowCreated(view) {
  injectStyle();
  view.setAttribute?.("data-title", "QQPortable");
  view.innerHTML = `
    <div class="qqportable-settings">
      <setting-section data-title="开机启动">
        <setting-panel>
          <div class="qp-row">
            <div class="qp-copy">
              <div class="qp-title">登录 Windows 后自动启动 QQ</div>
              <div class="qp-description">运行当前便携目录的 QQPortable.exe，不需要管理员权限。</div>
            </div>
            <label class="qp-switch">
              <input type="checkbox" aria-label="登录 Windows 后自动启动 QQ">
              <span class="qp-track"></span>
            </label>
          </div>
          <div class="qp-footer">
            <span class="qp-message" role="status" aria-live="polite"></span>
          </div>
        </setting-panel>
      </setting-section>
      <setting-section data-title="重启 QQ">
        <setting-panel>
          <div class="qp-row">
            <div class="qp-copy">
              <div class="qp-title">从“更多”菜单重启</div>
              <div class="qp-description">点击 QQ 左下角三横线“更多”，选择“设置”下方的“重启 QQ”。启动器会结束当前会话并重新启动，未发送的输入可能丢失。</div>
            </div>
          </div>
        </setting-panel>
      </setting-section>
      <div class="qp-path"></div>
    </div>
  `;

  const input = view.querySelector(".qp-switch input");
  const message = view.querySelector(".qp-message");
  const pathLabel = view.querySelector(".qp-path");
  let loading = true;

  const setMessage = (text, kind = "") => {
    message.textContent = text;
    message.dataset.kind = kind;
  };

  const api = window.qqPortableSettings;
  if (!api) {
    input.disabled = true;
    setMessage("插件桥接未加载，无法读取配置。", "error");
    return;
  }

  api
    .getConfig()
    .then((config) => {
      loading = false;
      input.checked = config.autoStart === true;
      pathLabel.textContent = "配置文件：" + config.configPath;
    })
    .catch((error) => {
      loading = false;
      input.disabled = true;
      setMessage(error?.message || String(error), "error");
    });

  input.addEventListener("change", async () => {
    if (loading) return;
    const enabled = input.checked;
    input.disabled = true;
    setMessage("正在保存…");
    try {
      const result = await api.setAutoStart(enabled);
      setMessage(result.autoStart ? "已开启开机自启。" : "已关闭开机自启。", "success");
    } catch (error) {
      input.checked = !enabled;
      setMessage(error?.message || String(error), "error");
    } finally {
      input.disabled = false;
    }
  });
}

initMoreMenuRestart();

export { onSettingWindowCreated };
