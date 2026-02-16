using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace StalkingStairsModManager.Services
{
    public static class BepInExInstaller
    {
        private const string Url =
            "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.2/BepInEx_win_x64_5.4.23.2.zip";

        public static async Task InstallAsync(string gamePath)
        {
            string zipPath = Path.Combine(gamePath, "bepinex.zip");

            using HttpClient client = new HttpClient();
            var data = await client.GetByteArrayAsync(Url);
            await File.WriteAllBytesAsync(zipPath, data);

            ZipFile.ExtractToDirectory(zipPath, gamePath, true);
            File.Delete(zipPath);
        }
    }
}
