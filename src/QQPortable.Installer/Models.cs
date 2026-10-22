using System;

namespace QQPortable.Installer
{
    internal sealed class QqAsset
    {
        public string Version { get; set; }
        public string DisplayName { get; set; }
        public string Url { get; set; }
        public long Size { get; set; }
        public string ReleaseName { get; set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    internal sealed class LiteLoaderRelease
    {
        public string Tag { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public long Size { get; set; }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Name) ? Tag : Name + " (" + Tag + ")";
        }
    }

    internal sealed class InstallOptions
    {
        public string TargetDirectory { get; set; }
        public string QQInstallerPath { get; set; }
        public string QQVersion { get; set; }
        public string LiteLoaderVersion { get; set; }
        public string LiteLoaderDirectory { get; set; }
        public bool Force { get; set; }
    }

    internal sealed class InstallResult
    {
        public string Summary { get; set; }
    }
}
