using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Xml.Linq;

namespace QQPortable.Installer
{
    internal static class GitHubCatalog
    {
        private const string QqRepository = "a1515333102/QQ-Releases";
        private const string QqHistoryRepository = "Rodert/qq-versions";
        private const string LiteLoaderRepository = "LiteLoaderQQNT/LiteLoaderQQNT";
        private static readonly Regex QqAssetPattern = new Regex(
            @"^QQ_(?<version>\d+\.\d+\.\d+)_\d+_x64_01\.exe$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex QqFilePattern = new Regex(
            @"QQ_(?<version>\d+\.\d+\.\d+)_\d+_x64_01\.exe",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly XNamespace AtomNamespace = "http://www.w3.org/2005/Atom";

        public static async Task<IReadOnlyList<QqAsset>> GetQqAssetsAsync()
        {
            List<QqAsset> assets = new List<QqAsset>();
            foreach (string repository in new[] { QqRepository, QqHistoryRepository })
            {
                try
                {
                    string json = await GetReleasesJsonAsync(repository).ConfigureAwait(false);
                    assets.AddRange(ParseQqAssets(json));
                }
                catch (Exception error) when (error is GitHubRateLimitException || error is HttpRequestException || error is TaskCanceledException)
                {
                    assets.AddRange(await GetQqAssetsFromPublishPageAsync(repository).ConfigureAwait(false));
                }
            }

            return assets
                .GroupBy(asset => asset.Version)
                .Select(group => group.First())
                .OrderByDescending(asset => Version.TryParse(asset.Version, out Version parsed) ? parsed : new Version(0, 0))
                .ToList();
        }

        internal static IReadOnlyList<QqAsset> ParseQqAssets(string json)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            object[] releases = serializer.DeserializeObject(json) as object[];
            List<QqAsset> result = new List<QqAsset>();
            if (releases == null)
            {
                return result;
            }

            foreach (Dictionary<string, object> release in releases.OfType<Dictionary<string, object>>())
            {
                string releaseName = GetString(release, "name");
                object[] assets = GetArray(release, "assets");
                foreach (Dictionary<string, object> asset in assets.OfType<Dictionary<string, object>>())
                {
                    string name = GetString(asset, "name");
                    Match match = QqAssetPattern.Match(name);
                    if (!match.Success)
                    {
                        continue;
                    }

                    result.Add(new QqAsset
                    {
                        Version = match.Groups["version"].Value,
                        DisplayName = match.Groups["version"].Value + " (" + name + ")",
                        Url = GetString(asset, "url"),
                        Size = GetLong(asset, "size"),
                        ReleaseName = releaseName,
                    });
                }
            }

            return result
                .GroupBy(asset => asset.Version)
                .Select(group => group.First())
                .OrderByDescending(asset => Version.TryParse(asset.Version, out Version parsed) ? parsed : new Version(0, 0))
                .ToList();
        }

        public static async Task<IReadOnlyList<LiteLoaderRelease>> GetLiteLoaderReleasesAsync()
        {
            try
            {
                string json = await GetReleasesJsonAsync(LiteLoaderRepository).ConfigureAwait(false);
                return ParseLiteLoaderReleases(json);
            }
            catch (Exception error) when (error is GitHubRateLimitException || error is HttpRequestException || error is TaskCanceledException)
            {
                return await GetLiteLoaderReleasesFromPublishPageAsync().ConfigureAwait(false);
            }
        }

        internal static IReadOnlyList<LiteLoaderRelease> ParseLiteLoaderReleases(string json)
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            object[] releases = serializer.DeserializeObject(json) as object[];
            List<LiteLoaderRelease> result = new List<LiteLoaderRelease>();
            if (releases == null)
            {
                return result;
            }

            foreach (Dictionary<string, object> release in releases.OfType<Dictionary<string, object>>())
            {
                string tag = GetString(release, "tag_name");
                string releaseName = GetString(release, "name");
                object[] assets = GetArray(release, "assets");
                Dictionary<string, object> zip = assets
                    .OfType<Dictionary<string, object>>()
                    .FirstOrDefault(asset => GetString(asset, "name").EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
                if (zip == null)
                {
                    continue;
                }

                result.Add(new LiteLoaderRelease
                {
                    Tag = tag,
                    Name = releaseName,
                    Url = GetString(zip, "url"),
                    Size = GetLong(zip, "size"),
                });
            }

            return result;
        }

        private static async Task<IReadOnlyList<QqAsset>> GetQqAssetsFromPublishPageAsync(string repository)
        {
            IReadOnlyList<ReleaseEntry> entries = await GetAtomEntriesAsync(repository).ConfigureAwait(false);
            List<QqAsset> assets = new List<QqAsset>();
            foreach (ReleaseEntry entry in entries)
            {
                HashSet<string> seenFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Match match in QqFilePattern.Matches(entry.Content))
                {
                    string file = match.Value;
                    if (!seenFiles.Add(file)) continue;
                    string version = match.Groups["version"].Value;
                    assets.Add(new QqAsset
                    {
                        Version = version,
                        DisplayName = version + " (" + file + ")",
                        Url = ReleaseDownloadUrl(repository, entry.Tag, file),
                        ReleaseName = entry.Title,
                    });
                }
            }

            return assets
                .GroupBy(asset => asset.Version)
                .Select(group => group.First())
                .OrderByDescending(asset => Version.TryParse(asset.Version, out Version parsed) ? parsed : new Version(0, 0))
                .ToList();
        }

        private static async Task<IReadOnlyList<LiteLoaderRelease>> GetLiteLoaderReleasesFromPublishPageAsync()
        {
            IReadOnlyList<ReleaseEntry> entries = await GetAtomEntriesAsync(LiteLoaderRepository).ConfigureAwait(false);
            return entries
                .GroupBy(entry => entry.Tag)
                .Select(group => group.First())
                .Select(entry => new LiteLoaderRelease
                {
                    Tag = entry.Tag,
                    Name = entry.Title,
                    Url = ReleaseDownloadUrl(LiteLoaderRepository, entry.Tag, "LiteLoaderQQNT.zip"),
                })
                .ToList();
        }

        private static async Task<IReadOnlyList<ReleaseEntry>> GetAtomEntriesAsync(string repository)
        {
            using (HttpClient client = CreateHtmlClient())
            {
                string xml = await client.GetStringAsync("https://github.com/" + repository + "/releases.atom").ConfigureAwait(false);
                XDocument document = XDocument.Parse(xml);
                List<ReleaseEntry> entries = new List<ReleaseEntry>();
                foreach (XElement element in document.Root.Elements(AtomNamespace + "entry"))
                {
                    XElement link = element.Elements(AtomNamespace + "link")
                        .FirstOrDefault(candidate => (string)candidate.Attribute("rel") == "alternate");
                    string href = (string)link?.Attribute("href");
                    if (string.IsNullOrEmpty(href)) continue;
                    const string marker = "/releases/tag/";
                    int markerIndex = href.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
                    if (markerIndex < 0) continue;
                    string tag = Uri.UnescapeDataString(href.Substring(markerIndex + marker.Length));
                    entries.Add(new ReleaseEntry
                    {
                        Tag = tag,
                        Title = (string)element.Element(AtomNamespace + "title") ?? tag,
                        Content = (string)element.Element(AtomNamespace + "content") ?? string.Empty,
                    });
                }
                return entries;
            }
        }

        private static string ReleaseDownloadUrl(string repository, string tag, string file)
        {
            return "https://github.com/" + repository + "/releases/download/"
                + Uri.EscapeDataString(tag) + "/" + Uri.EscapeDataString(file);
        }

        public static async Task DownloadAsync(string url, string destination, Action<string> log)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (HttpClient client = CreateClient())
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/octet-stream"));
                using (HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    long? total = response.Content.Headers.ContentLength;
                    using (System.IO.Stream input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (System.IO.FileStream output = new System.IO.FileStream(destination, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None))
                    {
                        byte[] buffer = new byte[1024 * 128];
                        long received = 0;
                        int read;
                        int lastPercent = -1;
                        while ((read = await input.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) > 0)
                        {
                            await output.WriteAsync(buffer, 0, read).ConfigureAwait(false);
                            received += read;
                            if (total.HasValue && total.Value > 0)
                            {
                                int percent = (int)(received * 100 / total.Value);
                                if (percent != lastPercent && percent % 5 == 0)
                                {
                                    lastPercent = percent;
                                    log?.Invoke("下载中 " + percent + "%");
                                }
                            }
                        }
                    }
                }
            }
        }

        private static async Task<string> GetReleasesJsonAsync(string repository)
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            using (HttpClient client = CreateClient())
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get,
                "https://api.github.com/repos/" + repository + "/releases?per_page=30"))
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                string token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
                if (string.IsNullOrWhiteSpace(token))
                {
                    token = Environment.GetEnvironmentVariable("GH_TOKEN");
                }
                if (!string.IsNullOrWhiteSpace(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                using (HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false))
                {
                    if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == (HttpStatusCode)429)
                    {
                        throw new GitHubRateLimitException("GitHub API 请求被拒绝，可能触发了未登录限流。");
                    }
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
        }

        private static HttpClient CreateClient()
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("QQPortableInstaller/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            client.Timeout = TimeSpan.FromMinutes(10);
            return client;
        }

        private static HttpClient CreateHtmlClient()
        {
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("QQPortableInstaller/1.0");
            client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/atom+xml,application/xml;q=0.9,*/*;q=0.8");
            client.Timeout = TimeSpan.FromSeconds(60);
            return client;
        }

        private sealed class ReleaseEntry
        {
            public string Tag { get; set; }
            public string Title { get; set; }
            public string Content { get; set; }
        }

        private static object[] GetArray(Dictionary<string, object> dictionary, string key)
        {
            return dictionary.TryGetValue(key, out object value) && value is object[] array ? array : new object[0];
        }

        private static string GetString(Dictionary<string, object> dictionary, string key)
        {
            return dictionary.TryGetValue(key, out object value) && value != null ? Convert.ToString(value) : string.Empty;
        }

        private static long GetLong(Dictionary<string, object> dictionary, string key)
        {
            return dictionary.TryGetValue(key, out object value) && value != null && long.TryParse(Convert.ToString(value), out long result) ? result : 0;
        }
    }

    internal sealed class GitHubRateLimitException : Exception
    {
        public GitHubRateLimitException(string message) : base(message)
        {
        }
    }
}
