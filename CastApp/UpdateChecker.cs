using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CastApp
{
    public class UpdateChecker
    {
        private readonly DiscordConfigService _configService;
        private readonly DiscordLogger _discordLogger;

        private readonly string MachineName = Environment.MachineName.ToString();
        private readonly string UserName = Environment.UserName.ToString();
        private readonly string OsVersion = Environment.OSVersion.VersionString.ToString();
        private readonly string CpuCount = Environment.ProcessorCount.ToString();

        private const string CURRENT_VERSION = "1.0.0";
        private const string VERSION_URL = "Url do json que sera criado no GitHub";

        public UpdateChecker(DiscordConfigService configService, DiscordLogger discordLogger)
        {
            _configService = configService;
            _discordLogger = discordLogger;
        }

        public async Task CheckForUpdatesAsync()
        {
            try
            {
                Program.LogError("Verificando Atualizações...");

                var updateInfo = await FetchLastetVersionAsync();

                if (updateInfo == null)
                {
                    await _discordLogger!.SendLogAsync(MachineName, UserName, OsVersion, CpuCount, "Não foi possível obter informações de atualização.");
                    return;
                }

                var currentVersion = new Version(CURRENT_VERSION);
                var latestVersion = new Version(updateInfo.Version);

                if (latestVersion > currentVersion)
                {
                    await DownLoadAndApplyUpdateAsync(updateInfo);
                }
                else
                {
                    return;
                }
            }
            catch (Exception ex)
            {
                await _discordLogger!.SendLogAsync(MachineName, UserName, OsVersion, CpuCount, $"Erro procurando por atualizações: {ex.Message}");
            }
        }

        private async Task<VersionInfo?> FetchLastetVersionAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("CastApp-Logger/1.0");
                client.Timeout = TimeSpan.FromSeconds(10);

                var response = await client.GetStringAsync(VERSION_URL);

                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                };

                var updateInfo = JsonSerializer.Deserialize<VersionInfo>(response, options);

                return updateInfo;
            }
            catch (Exception ex)
            {
                await _discordLogger!.SendLogAsync(MachineName, UserName, OsVersion, CpuCount, $"Erro ao obter a versão mais recente: {ex.Message}");
                return null;
            }
        }

        private async Task DownLoadAndApplyUpdateAsync(VersionInfo versionInfo)
        {
            try
            {
                using var client = new HttpClient();
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string zipFilePath = Path.Combine(appDirectory, "Update.zip");
                string appExecutable = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                string updaterPath = Path.Combine(appDirectory, "Updater.exe");

                if (!File.Exists(updaterPath))
                {
                    await _discordLogger!.SendLogAsync(MachineName, UserName, OsVersion, CpuCount, "Updater.exe não encontrado! Atualizador automático indisponível.");
                    return;
                }

                byte[] zipdata = await client.GetByteArrayAsync(versionInfo.UpdateUrl);
                await File.WriteAllBytesAsync(zipFilePath, zipdata);

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = updaterPath,
                    UseShellExecute = true,
                };
                startInfo.ArgumentList.Add(appDirectory);
                startInfo.ArgumentList.Add(zipFilePath);
                startInfo.ArgumentList.Add(appExecutable);
                startInfo.ArgumentList.Add("--silent");
                Process.Start(startInfo);
                await Task.Delay(2000);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                await _discordLogger!.SendLogAsync(MachineName, UserName, OsVersion, CpuCount, $"Erro ao baixar ou aplicar a atualização: {ex.Message}");
            }
        }
    }
}
