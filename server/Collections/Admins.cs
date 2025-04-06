using System.Security.Cryptography;

namespace server.Collections;

/// <summary>
/// Admin class. Represents objects from the admins collection in the database. 
/// </summary>
public class Admin
{
    public string Name { get; set; }
    public string Role { get; set; }
    public string Token { get; set; }

    public Admin(string name, string role, string token)
    {
        Name = name;
        Role = role;
        Token = token;
    }
}

public class Admins
{
    /// <summary>
    /// Asynchronously generates a 48 character Admin token.
    /// </summary>
    /// <returns>string token - result of the generation.</returns>
    public async Task<string> GenTokenAsync()
    {
        var tokenChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()_+-=[]{}<>,./~";
        var tokenLength = 48;
        return await Task.Run(() =>
        {
            using var rng = RandomNumberGenerator.Create();
            var randomBytes = new byte[tokenLength];
            rng.GetBytes(randomBytes);
            
            return new string(randomBytes.Select(b => tokenChars[b % tokenChars.Length]).ToArray());
        });
    }
}