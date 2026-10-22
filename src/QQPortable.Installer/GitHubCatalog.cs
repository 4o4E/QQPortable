using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace QQPortable.Installer
{
    internal static class GitHubCatalog
    {
        private const string QqRepository = "a1515333102/QQ-Releases";
        private const string LiteLoaderRepository = "LiteLoaderQQNT/LiteLoaderQQNT";
        private static readonly Regex QqAssetPattern = new Regex(
            @"^QQ_(?<version>\d+\.\d+\.\d+)_\d+_x64_01\.exe$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static async Task<IReadOnlyList<QqAsset>> GetQqAssetsAsync()
        {
            string json = await GetReleasesJsonAsync(QqRepository).ConfigureAwait(false);
            return ParseQqAssets(json);
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
            string json = await GetReleasesJsonAsync(LiteLoaderRepository).ConfigureAwait(false);
            return ParseLiteLoaderReleases(json);
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
            {
                string url = "https://api.github.com/repos/" + repository + "/releases?per_page=30";
                using (HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false))
                {
                    if (response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        throw new InvalidOperationException("GitHub API 请求被拒绝，可能触发了未登录限流。");
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
}
