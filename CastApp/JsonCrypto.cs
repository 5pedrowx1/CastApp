using System.Security.Cryptography;
using System.Text;
public static class JsonCrypto
{
    private const int SaltSize = 16;      // 128-bit salt
    private const int NonceSize = 12;     // 96-bit nonce for AES-GCM
    private const int TagSize = 16;       // 128-bit tag
    private const int KeySize = 32;       // 256-bit key
    private const int Iterations = 100_000; // PBKDF2 iterations

    private static byte[] DeriveKey(string passphrase, byte[] salt)
    {
        using var kdf = new Rfc2898DeriveBytes(passphrase, salt, Iterations, HashAlgorithmName.SHA256);
        return kdf.GetBytes(KeySize);
    }

    // Lê ficheiro criptografado e retorna JSON desincriptado
    public static async Task<string> DecryptJsonFromFileAsync(string encryptedFilePath, string passphrase)
    {
        if (encryptedFilePath is null) throw new ArgumentNullException(nameof(encryptedFilePath));
        if (passphrase is null) throw new ArgumentNullException(nameof(passphrase));
        byte[] fileBytes = await File.ReadAllBytesAsync(encryptedFilePath);
        if (fileBytes.Length < SaltSize + NonceSize + TagSize)
            throw new InvalidDataException("Ficheiro demasiado pequeno para ser válido.");
        int offset = 0;
        byte[] salt = new byte[SaltSize];
        Array.Copy(fileBytes, offset, salt, 0, SaltSize);
        offset += SaltSize;
        byte[] nonce = new byte[NonceSize];
        Array.Copy(fileBytes, offset, nonce, 0, NonceSize);
        offset += NonceSize;
        byte[] tag = new byte[TagSize];
        Array.Copy(fileBytes, offset, tag, 0, TagSize);
        offset += TagSize;
        int ciphertextLength = fileBytes.Length - offset;
        byte[] ciphertext = new byte[ciphertextLength];
        Array.Copy(fileBytes, offset, ciphertext, 0, ciphertextLength);
        byte[] key = DeriveKey(passphrase, salt);
        byte[] plaintext = new byte[ciphertext.Length];
        using (var aesGcm = new AesGcm(key, TagSize))
        {
            try
            {
                aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
            }
            catch (CryptographicException)
            {
                throw new CryptographicException("Falha na autenticação ou passphrase inválida.");
            }
        }
        return Encoding.UTF8.GetString(plaintext);
    }
}