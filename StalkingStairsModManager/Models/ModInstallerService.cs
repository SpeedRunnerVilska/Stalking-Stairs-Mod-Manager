using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using StalkingStairsModManager.Models;

namespace StalkingStairsModManager.Services
{
    public class ModInstallerService
    {
        private readonly HttpClient _client = new();

        public ModInstallerService()
        {
            // set a User-Agent so GitHub/API endpoints accept requests
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("StalkingStairsModManager/1.0");
        }

        public async Task InstallAsync(ModInfo mod, string gamePath)
        {
            if (string.IsNullOrWhiteSpace(mod.downloadUrl))
                throw new InvalidOperationException("No download URL for mod.");

            string pluginsPath = Path.Combine(gamePath, "BepInEx", "plugins");
            Directory.CreateDirectory(pluginsPath);

            var uri = new Uri(mod.downloadUrl);
            string fileName = Path.GetFileName(uri.LocalPath) ?? $"{SanitizeFileName(mod.name)}.bin";
            string lower = fileName.ToLowerInvariant();

            if (lower.EndsWith(".dll"))
            {
                string target = Path.Combine(pluginsPath, fileName);
                var bytes = await _client.GetByteArrayAsync(mod.downloadUrl);
                await File.WriteAllBytesAsync(target, bytes);
                return;
            }

            if (lower.EndsWith(".zip"))
            {
                string temp = Path.Combine(Path.GetTempPath(), $"mod_{Guid.NewGuid():N}.zip");
                var bytes = await _client.GetByteArrayAsync(mod.downloadUrl);
                await File.WriteAllBytesAsync(temp, bytes);

                string extractDir = Path.Combine(pluginsPath, SanitizeFileName(mod.name));
                // ensure clean install directory
                if (Directory.Exists(extractDir))
                    Directory.Delete(extractDir, true);

                ZipFile.ExtractToDirectory(temp, extractDir);
                File.Delete(temp);
                return;
            }

            // fallback: save file into plugins folder
            string fallbackPath = Path.Combine(pluginsPath, fileName);
            var fallbackBytes = await _client.GetByteArrayAsync(mod.downloadUrl);
            await File.WriteAllBytesAsync(fallbackPath, fallbackBytes);
        }

        public Task UninstallAsync(ModInfo mod, string gamePath)
        {
            string pluginsPath = Path.Combine(gamePath, "BepInEx", "plugins");
            if (!Directory.Exists(pluginsPath)) return Task.CompletedTask;

            try
            {
                // remove extracted folder if zip was used
                string extractedDir = Path.Combine(pluginsPath, SanitizeFileName(mod.name));
                if (Directory.Exists(extractedDir))
                {
                    Directory.Delete(extractedDir, true);
                    return Task.CompletedTask;
                }

                // otherwise remove any file matching the download filename
                if (!string.IsNullOrWhiteSpace(mod.downloadUrl))
                {
                    var uri = new Uri(mod.downloadUrl);
                    string fileName = Path.GetFileName(uri.LocalPath) ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(fileName))
                    {
                        string candidate = Path.Combine(pluginsPath, fileName);
                        if (File.Exists(candidate))
                            File.Delete(candidate);
                    }
                }
            }
            catch
            {
                // swallow; uninstall best-effort
            }

            return Task.CompletedTask;
        }

        private static string SanitizeFileName(string input)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                input = input.Replace(c, '_');
            return input.Trim();
        }
    }
}