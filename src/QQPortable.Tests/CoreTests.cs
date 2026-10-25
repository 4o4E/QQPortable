using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using QQPortable.Installer;
using QQPortable.Shared;

namespace QQPortable.Tests
{
    public sealed class CoreTests : IDisposable
    {
        private readonly string temporary = Path.Combine(Path.GetTempPath(), "QQPortableTests", Guid.NewGuid().ToString("N"));

        public CoreTests()
        {
            Directory.CreateDirectory(temporary);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
            }
            catch
            {
            }
        }

        [Fact]
        public void QqCatalogFiltersX64AndSortsVersions()
        {
            const string json = @"[
              {""name"":""QQ Windows 9.9.35"",""tag_name"":""9.9.35+3.2.33"",""assets"":[
                {""name"":""QQ_9.9.35_260902_x64_01.exe"",""url"":""https://api.github.com/assets/35"",""size"":300},
                {""name"":""QQ_9.9.35_260902_arm64_01.exe"",""url"":""https://api.github.com/assets/35-arm"",""size"":200}
              ]},
              {""name"":""QQ Windows 9.9.36"",""tag_name"":""9.9.36+3.2.34"",""assets"":[
                {""name"":""QQ_9.9.36_260924_x64_01.exe"",""url"":""https://api.github.com/assets/36"",""size"":400},
                {""name"":""QQ_9.9.36_260924_x86_01.exe"",""url"":""https://api.github.com/assets/36-x86"",""size"":100}
              ]}
            ]";

            IReadOnlyList<QqAsset> assets = GitHubCatalog.ParseQqAssets(json);

            Assert.Equal(2, assets.Count);
            Assert.Equal("9.9.36", assets[0].Version);
            Assert.Equal("https://api.github.com/assets/36", assets[0].Url);
            Assert.Equal("9.9.35", assets[1].Version);
        }

        [Fact]
        public void LiteLoaderCatalogUsesZipAsset()
        {
            const string json = @"[
              {""name"":""LiteLoaderQQNT 1.4.1"",""tag_name"":""1.4.1"",""assets"":[
                {""name"":""LiteLoaderQQNT.zip"",""url"":""https://api.github.com/assets/ll"",""size"":32911},
                {""name"":""checksums.txt"",""url"":""https://api.github.com/assets/sums"",""size"":50}
              ]}
            ]";

            IReadOnlyList<LiteLoaderRelease> releases = GitHubCatalog.ParseLiteLoaderReleases(json);

            Assert.Single(releases);
            Assert.Equal("1.4.1", releases[0].Tag);
            Assert.Equal("https://api.github.com/assets/ll", releases[0].Url);
        }

        [Fact]
        public void FindsAllSevenZipSignatures()
        {
            string path = Path.Combine(temporary, "installer.exe");
            byte[] data = new byte[256];
            byte[] signature = { 0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C };
            Buffer.BlockCopy(signature, 0, data, 100, signature.Length);
            Buffer.BlockCopy(signature, 0, data, 180, signature.Length);
            File.WriteAllBytes(path, data);

            List<long> offsets = InstallerCore.Find7zOffsets(path);

            Assert.Equal(new long[] { 100, 180 }, offsets);
        }

        [Fact]
        public void AppliesLiteLoaderPatchesAndIsIdempotent()
        {
            string root = Path.Combine(temporary, "LiteLoader");
            Directory.CreateDirectory(Path.Combine(root, "src", "main"));
            Directory.CreateDirectory(Path.Combine(root, "src", "preload"));
            File.WriteAllText(Path.Combine(root, "src", "main", "store.js"),
                "const list = dirs.map(dirent => path.join(dirent.path, dirent.name, \"manifest.json\"));\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(root, "src", "preload.js"),
                "const { contextBridge } = require(\"electron\");\nconst { Runtime } = require(\"./preload/runtime.js\");\nfor (const plugin of []) {}\ncontextBridge.executeInMainWorld({ func: () => {} });\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(root, "src", "preload", "module.js"),
                "for (const ext of [\"\", \".js\", \".json\"]) {}\nswitch (ext) { case \".js\": return load(); }\n", new UTF8Encoding(false));

            LiteLoaderPatcher.Apply(root, _ => { });
            LiteLoaderPatcher.Apply(root, _ => { });

            string store = File.ReadAllText(Path.Combine(root, "src", "main", "store.js"));
            string preload = File.ReadAllText(Path.Combine(root, "src", "preload.js"));
            string module = File.ReadAllText(Path.Combine(root, "src", "preload", "module.js"));
            Assert.Contains("dirent.parentPath ?? dirent.path", store);
            Assert.Contains("login.html", preload);
            Assert.Equal(1, CountOccurrences(preload, "login.html"));
            Assert.Contains(".cjs", module);
            Assert.Equal(1, CountOccurrences(module, "case \".cjs\""));
        }

        [Fact]
        public void RejectsUnsupportedLiteLoaderSource()
        {
            string root = Path.Combine(temporary, "LiteLoaderUnsupported");
            Directory.CreateDirectory(Path.Combine(root, "src", "main"));
            Directory.CreateDirectory(Path.Combine(root, "src", "preload"));
            File.WriteAllText(Path.Combine(root, "src", "main", "store.js"), "unknown();\n");
            File.WriteAllText(Path.Combine(root, "src", "preload.js"), "unknown();\n");
            File.WriteAllText(Path.Combine(root, "src", "preload", "module.js"), "unknown();\n");

            Assert.Throws<InvalidOperationException>(() => LiteLoaderPatcher.Apply(root, _ => { }));
        }

        [Fact]
        public void PortableConfigWritesDefaultWhenMissing()
        {
            string root = Path.Combine(temporary, "portable-default");
            Directory.CreateDirectory(root);

            PortableConfig config = PortableConfig.LoadOrCreate(root);

            Assert.False(config.AutoStart);
            string path = Path.Combine(root, "QQPortable.json");
            Assert.True(File.Exists(path));
            string text = File.ReadAllText(path);
            Assert.Contains("\"autoStart\": false", text);
        }

        [Fact]
        public void PortableConfigPreservesUnknownFields()
        {
            string root = Path.Combine(temporary, "portable-preserve");
            Directory.CreateDirectory(root);
            string path = Path.Combine(root, "QQPortable.json");
            File.WriteAllText(path,
                "{\"autoStart\":true,\"note\":\"keep\",\"nested\":{\"value\":1}}",
                new UTF8Encoding(false));

            PortableConfig config = PortableConfig.LoadOrCreate(root);
            Assert.True(config.AutoStart);
            config.AutoStart = false;
            config.Save();

            string text = File.ReadAllText(path);
            Assert.Contains("\"note\": \"keep\"", text);
            Assert.Contains("\"nested\"", text);
            Assert.Contains("\"autoStart\": false", text);
            Assert.False(PortableConfig.LoadOrCreate(root).AutoStart);
        }

        [Fact]
        public void PortableConfigRejectsInvalidJson()
        {
            string root = Path.Combine(temporary, "portable-invalid");
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "QQPortable.json"), "{", new UTF8Encoding(false));

            Assert.Throws<InvalidDataException>(() => PortableConfig.LoadOrCreate(root));
        }

        [Fact]
        public void PortableConfigRejectsNonBooleanAutoStart()
        {
            string root = Path.Combine(temporary, "portable-type");
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "QQPortable.json"),
                "{\"autoStart\":\"true\"}",
                new UTF8Encoding(false));

            PortableConfig config = PortableConfig.LoadOrCreate(root);

            Assert.Throws<InvalidDataException>(() => config.AutoStart);
        }

        [Fact]
        public void AutoStartCommandQuotesExecutablePath()
        {
            string command = AutoStartRegistration.BuildCommand(@"C:\Program Files\QQPortable");

            Assert.Equal("\"C:\\Program Files\\QQPortable\\QQPortable.exe\"", command);
        }

        [Fact]
        public void ExtractsSettingsAndPluginAssets()
        {
            string root = Path.Combine(temporary, "extracted");
            Directory.CreateDirectory(root);

            EmbeddedAssets.ExtractLauncher(root);
            EmbeddedAssets.ExtractPlugin(root);

            string[] files =
            {
                Path.Combine(root, "QQPortable.exe"),
                Path.Combine(root, "QQPortable.setting.exe"),
                Path.Combine(root, "Launcher", "NapCatWinBootMain.exe"),
                Path.Combine(root, "LiteLoader", "plugins", "qqportable_settings", "manifest.json"),
                Path.Combine(root, "LiteLoader", "plugins", "qqportable_settings", "src", "main.cjs"),
                Path.Combine(root, "LiteLoader", "plugins", "qqportable_settings", "src", "preload.cjs"),
                Path.Combine(root, "LiteLoader", "plugins", "qqportable_settings", "src", "renderer.mjs"),
            };
            foreach (string file in files)
            {
                Assert.True(File.Exists(file), file);
                Assert.True(new FileInfo(file).Length > 0, file);
            }
        }

        private static int CountOccurrences(string text, string needle)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }
            return count;
        }
    }
}
