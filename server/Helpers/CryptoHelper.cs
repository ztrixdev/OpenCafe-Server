using System.Security.Cryptography;

namespace server.Helpers;

public class CryptoHelper
{
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
}