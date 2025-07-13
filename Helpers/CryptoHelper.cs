using System.Security.Cryptography;

namespace OpenCafe.Server.Helpers;

/// <summary>
/// Cryptography helper class. 
/// </summary>
public static class CryptoHelper
{
    public static string key = "key";

    /// <summary>
    /// Converts a plain string into a base64 encrypted string.
    /// </summary>
    /// <param name="plainText"></param>
    /// <param name="key"></param>
    /// <returns>Encrypted base64 string</returns>
    /// <exception cref="ArgumentException">Refer to the message for info</exception>
    public static async Task<string> EncryptAsync(string plainText, string key)
    {
        if (string.IsNullOrEmpty(plainText))
            throw new ArgumentException("Plain text cannot be null or empty.");
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("A Key must be provided.");

        byte[] encryptedBytes;

        var iv = await RandomBase64Async();
        using (Aes aes = Aes.Create())
        {
            aes.Key = Convert.FromBase64String(key);
            aes.IV = Convert.FromBase64String(iv);

            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using MemoryStream ms = new();
            await using CryptoStream cs = new(ms, encryptor, CryptoStreamMode.Write);
            await using (StreamWriter sw = new(cs))
            {
                await sw.WriteAsync(plainText);
            }
            encryptedBytes = ms.ToArray();
        }

        return $"{Convert.ToBase64String(encryptedBytes)}{iv}";
    }

    /// <summary>
    /// Converts a base64 encrypted string into plain text
    /// </summary>
    /// <param name="cipherText"></param>
    /// <param name="key"></param>
    /// <returns>Decrypted string</returns>
    /// <exception cref="ArgumentException">Refer to the message for info</exception>
    public static async Task<string> DecryptAsync(string cipherText, string key)
    {
        if (string.IsNullOrEmpty(cipherText))
            throw new ArgumentException("Cipher text cannot be null or empty.");
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key and IV must be provided.");

        string decryptedText;

        using (Aes aes = Aes.Create())
        {
            aes.Key = Convert.FromBase64String(key);
            aes.IV = Convert.FromBase64String(cipherText[^24..]);

            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            byte[] cipherBytes = Convert.FromBase64String(cipherText);

            using MemoryStream ms = new(cipherBytes);
            await using CryptoStream cs = new(ms, decryptor, CryptoStreamMode.Read);
            using StreamReader sr = new(cs);
            decryptedText = await sr.ReadToEndAsync();
        }

        return decryptedText;
    }

    // Old single iv encryption wont work with the dbcfg, so, ill just leave the old methods here so it works. Might replace them in the future.
    public static async Task<string> EncryptDBCfgAsync(string rawDBCfg, string key, string iv)
    {
        if (string.IsNullOrEmpty(rawDBCfg))
            throw new ArgumentException("Plain text cannot be null or empty.");
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(iv))
            throw new ArgumentException("Key and IV must be provided.");

        byte[] encryptedBytes;

        using (Aes aes = Aes.Create())
        {
            aes.Key = Convert.FromBase64String(key);
            aes.IV = Convert.FromBase64String(iv);

            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using MemoryStream ms = new();
            await using CryptoStream cs = new(ms, encryptor, CryptoStreamMode.Write);
            await using (StreamWriter sw = new(cs))
            {
                await sw.WriteAsync(rawDBCfg);
            }
            encryptedBytes = ms.ToArray();
        }

        return Convert.ToBase64String(encryptedBytes);
    }

    public static async Task<string> DecryptDBCfgAsync(string encDBCfg, string key, string iv)
    {
        if (string.IsNullOrEmpty(encDBCfg))
            throw new ArgumentException("Cipher text cannot be null or empty.");
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(iv))
            throw new ArgumentException("Key and IV must be provided.");

        string decryptedText;

        using (Aes aes = Aes.Create())
        {
            aes.Key = Convert.FromBase64String(key);
            aes.IV = Convert.FromBase64String(iv);

            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            byte[] cipherBytes = Convert.FromBase64String(encDBCfg);

            using MemoryStream ms = new(cipherBytes);
            await using CryptoStream cs = new(ms, decryptor, CryptoStreamMode.Read);
            using StreamReader sr = new(cs);
            decryptedText = await sr.ReadToEndAsync();
        }

        return decryptedText;
    }

    public static async Task<string> RandomBase64Async()
    {
        return await Task.Run(() =>
        {
            var randomBytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }
            return Convert.ToBase64String(randomBytes);
        });
    }
}
