using System.Security.Cryptography;

namespace server.Collections;

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
    public async Task<string> GenTokenAsync()
    {
        var TokenChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()_+-=[]{}<>,./~";
        var TokenLength = 48;
        return await Task.Run(() =>
        {
            using var rng = RandomNumberGenerator.Create();
            var randomBytes = new byte[TokenLength];
            rng.GetBytes(randomBytes);
            
            return new string(randomBytes.Select(b => TokenChars[b % TokenChars.Length]).ToArray());
        });
    }
}