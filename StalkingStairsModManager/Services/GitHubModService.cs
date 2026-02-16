using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using StalkingStairsModManager.Models;

namespace StalkingStairsModManager.Services
{
    public class GitHubModService
    {
        // Updated to the raw URL for the new repo location
        private const string ModsUrl =
            "https://raw.githubusercontent.com/SpeedRunnerVilska/Stalking-Stairs-Mod-Manager/main/mods.json";

        private const string ForcedBepInExUrl =
            "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.2/BepInEx_win_x64_5.4.23.2.zip";

        public async Task<List<ModInfo>> GetModsAsync()
        {
            using HttpClient client = new();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("StalkingStairsModManager", "1.0"));

            string json = null;
            const int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    json = await client.GetStringAsync(ModsUrl);
                }
                catch (Exception) when (attempt < maxAttempts)
                {
                    await Task.Delay(1000);
                    continue;
                }
                break;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException($"Failed to download mods manifest from {ModsUrl}");
            }

            // Quick heuristic: if it looks truncated (no closing ']' or '}'), try one more download
            if (!json.TrimEnd().EndsWith("]") && !json.TrimEnd().EndsWith("}"))
            {
                try
                {
                    json = await client.GetStringAsync(ModsUrl);
                }
                catch { /* ignore, will log below */ }
            }

            // Save raw response for troubleshooting
            string rawLogPath = SaveRawManifestForDebug(json);

            // Determine json content to deserialize: root array or object.mods
            string jsonForDeserialize = json;
            try
            {
                using var root = JsonDocument.Parse(json);
                if (root.RootElement.ValueKind == JsonValueKind.Array)
                {
                    // good: json is the array already
                }
                else if (root.RootElement.ValueKind == JsonValueKind.Object && root.RootElement.TryGetProperty("mods", out var modsElement) && modsElement.ValueKind == JsonValueKind.Array)
                {
                    jsonForDeserialize = modsElement.GetRawText();
                }
                else
                {
                    throw new JsonException($"Mods manifest does not contain an array at root or a 'mods' array. Raw saved to: {rawLogPath}");
                }
            }
            catch (JsonException ex)
            {
                throw new JsonException($"Failed to parse the mods manifest. Raw saved to: {rawLogPath}", ex);
            }

            List<ModInfo> mods;
            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                mods = JsonSerializer.Deserialize<List<ModInfo>>(jsonForDeserialize, options) ?? new List<ModInfo>();
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Failed to deserialize mods manifest. Raw saved to: {rawLogPath}", ex);
            }

            // Resolve GitHub assets where possible (non-fatal)
            using HttpClient githubClient = new();
            githubClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("StalkingStairsModManager", "1.0"));
            foreach (var mod in mods)
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(mod.downloadUrl) && IsGitHubUrl(mod.downloadUrl))
                    {
                        await TryResolveLatestGitHubReleaseAsync(mod, githubClient);
                    }
                }
                catch
                {
                    // per-mod resolution errors are non-fatal
                }
            }

            // Ensure BepInEx is present and forced
            var bepinex = mods.Find(m => string.Equals(m.name, "BepInEx", StringComparison.OrdinalIgnoreCase)
                                         || (m.downloadUrl?.IndexOf("BepInEx", StringComparison.OrdinalIgnoreCase) >= 0));
            if (bepinex == null)
            {
                bepinex = new ModInfo
                {
                    name = "BepInEx",
                    author = "BepInEx",
                    version = "5.4.23.2",
                    downloadUrl = ForcedBepInExUrl,
                    description = "BepInEx runtime (forced)",
                    enabled = true
                };
                mods.Insert(0, bepinex);
            }
            else
            {
                bepinex.enabled = true;
                bepinex.downloadUrl = ForcedBepInExUrl;
                bepinex.version = "5.4.23.2";
            }

            return mods;
        }

        private static string SaveRawManifestForDebug(string json)
        {
            try
            {
                string baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StalkingStairsModManager", "logs");
                Directory.CreateDirectory(baseDir);
                string fileName = $"manifest_{DateTime.UtcNow:yyyyMMddHHmmss}.json";
                string path = Path.Combine(baseDir, fileName);
                File.WriteAllText(path, json ?? string.Empty);
                return path;
            }
            catch
            {
                return "Unable to write raw manifest to log folder";
            }
        }

        private static bool IsGitHubUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;
            return uri.Host.IndexOf("github.com", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static async Task TryResolveLatestGitHubReleaseAsync(ModInfo mod, HttpClient client)
        {
            if (!Uri.TryCreate(mod.downloadUrl, UriKind.Absolute, out var uri))
                return;

            var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length < 2)
                return;

            var owner = segments[0];
            var repo = segments[1];

            var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

            using var resp = await client.GetAsync(apiUrl);
            if (!resp.IsSuccessStatusCode)
                return;

            using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            if (doc.RootElement.TryGetProperty("tag_name", out var tagElement))
            {
                var tag = tagElement.GetString() ?? string.Empty;
                mod.version = tag.TrimStart('v');
            }

            if (doc.RootElement.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                string? chosenUrl = null;

                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.TryGetProperty("browser_download_url", out var urlProp))
                    {
                        var assetUrl = urlProp.GetString();
                        if (assetUrl != null && assetUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            chosenUrl = assetUrl;
                            break;
                        }

                        if (chosenUrl == null && assetUrl != null)
                            chosenUrl = assetUrl;
                    }
                }

                if (!string.IsNullOrEmpty(chosenUrl))
                {
                    mod.downloadUrl = chosenUrl;
                }
            }
        }
    }
}