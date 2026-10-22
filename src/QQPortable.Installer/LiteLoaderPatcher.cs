using System;
using System.IO;
using System.Text;

namespace QQPortable.Installer
{
    internal static class LiteLoaderPatcher
    {
        public static void Apply(string liteLoaderDirectory, Action<string> log)
        {
            string storePath = Path.Combine(liteLoaderDirectory, "src", "main", "store.js");
            string preloadPath = Path.Combine(liteLoaderDirectory, "src", "preload.js");
            string modulePath = Path.Combine(liteLoaderDirectory, "src", "preload", "module.js");
            PatchStore(storePath, log);
            PatchPreload(preloadPath, log);
            PatchModule(modulePath, log);
        }

        private static void PatchStore(string path, Action<string> log)
        {
            string text = Read(path);
            if (text.Contains("dirent.parentPath ?? dirent.path"))
            {
                return;
            }

            string patched = text.Replace(
                "path.join(dirent.path, dirent.name, \"manifest.json\")",
                "path.join(dirent.parentPath ?? dirent.path, dirent.name, \"manifest.json\")");
            if (patched == text)
            {
                throw new InvalidOperationException("LiteLoader 版本未适配：store.js 的目录项写法不匹配。");
            }

            Write(path, patched);
            log?.Invoke("已应用 LiteLoader store.js 补丁");
        }

        private static void PatchPreload(string path, Action<string> log)
        {
            string text = Read(path);
            if (text.Contains("login.html"))
            {
                return;
            }

            const string marker = "const { Runtime } = require(\"./preload/runtime.js\");";
            int index = text.IndexOf(marker, StringComparison.Ordinal);
            if (index < 0)
            {
                throw new InvalidOperationException("LiteLoader 版本未适配：preload.js 缺少 Runtime 引用。");
            }

            string patched = text.Insert(
                index + marker.Length,
                "\n\nif (!String(location.href).includes(\"login.html\")) {");
            patched = patched.TrimEnd() + "\n}\n";
            Write(path, patched);
            log?.Invoke("已应用 LiteLoader 登录页跳过补丁");
        }

        private static void PatchModule(string path, Action<string> log)
        {
            string text = Read(path);
            bool changed = false;
            if (!text.Contains(".cjs"))
            {
                string listPattern = "[\"\", \".js\", \".json\"]";
                string listPatched = text.Replace(listPattern, "[\"\", \".js\", \".cjs\", \".json\"]");
                if (listPatched == text)
                {
                    throw new InvalidOperationException("LiteLoader 版本未适配：module.js 的扩展名列表不匹配。");
                }
                text = listPatched;
                changed = true;
            }

            if (!text.Contains("case \".cjs\""))
            {
                string casePattern = "case \".js\":";
                string casePatched = text.Replace(casePattern, "case \".js\": case \".cjs\":");
                if (casePatched == text)
                {
                    throw new InvalidOperationException("LiteLoader 版本未适配：module.js 的 .js 分支不匹配。");
                }
                text = casePatched;
                changed = true;
            }

            if (changed)
            {
                Write(path, text);
                log?.Invoke("已应用 LiteLoader .cjs 预加载补丁");
            }
        }

        private static string Read(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("LiteLoader 文件缺失：" + path, path);
            }
            return File.ReadAllText(path, Encoding.UTF8);
        }

        private static void Write(string path, string text)
        {
            File.WriteAllText(path, text, new UTF8Encoding(false));
        }
    }
}
