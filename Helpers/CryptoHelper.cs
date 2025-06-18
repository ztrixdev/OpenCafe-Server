using System.Security.Cryptography;

namespace server.Helpers;

/// <summary>
/// Cryptography helper class. 
/// </summary>
public static class CryptoHelper
{
    /// <summary>
    /// Converts a plain string into a base64 encrypted string.
    /// </summary>
    /// <param name="plainText"></param>
    /// <param name="key"></param>
    /// <param name="iv"></param>
    /// <returns>Encrypted base64 string</returns>
    /// <exception cref="ArgumentException">Refer to the message for info</exception>
    public static async Task<string> EncryptAsync(string plainText, string key, string iv)
    {
        if (string.IsNullOrEmpty(plainText))
            throw new ArgumentException("Plain text cannot be null or empty.");
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(iv))
            throw new ArgumentException("Key and IV must be provided.");

        byte[] encryptedBytes;

        using (Aes aes = Aes.Create())
        {
            aes.Key = Convert.FromBase64String(key);
            aes.IV = Convert.FromBase64String(iv);

            ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

            using (MemoryStream ms = new MemoryStream())
            {
                await using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    await using (StreamWriter sw = new StreamWriter(cs))
                    {
                        await sw.WriteAsync(plainText);
                    }
                    encryptedBytes = ms.ToArray();
                }
            }
        }

        return Convert.ToBase64String(encryptedBytes);
    }

    /// <summary>
    /// Converts a base64 encrypted string into plain text
    /// </summary>
    /// <param name="cipherText"></param>
    /// <param name="key"></param>
    /// <param name="iv"></param>
    /// <returns>Decrypted string</returns>
    /// <exception cref="ArgumentException">Refer to the message for info</exception>
    public static async Task<string> DecryptAsync(string cipherText, string key, string iv)
    {
        if (string.IsNullOrEmpty(cipherText))
            throw new ArgumentException("Cipher text cannot be null or empty.");
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(iv))
            throw new ArgumentException("Key and IV must be provided.");

        string decryptedText;

        using (Aes aes = Aes.Create())
        {
            aes.Key = Convert.FromBase64String(key);
            aes.IV = Convert.FromBase64String(iv);

            ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

            byte[] cipherBytes = Convert.FromBase64String(cipherText);

            using (MemoryStream ms = new MemoryStream(cipherBytes))
            {
                await using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                {
                    using (StreamReader sr = new StreamReader(cs))
                    {
                        decryptedText = await sr.ReadToEndAsync();
                    }
                }
            }
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
