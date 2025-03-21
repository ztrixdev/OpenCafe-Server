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
    public async Task<string> GenToken()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*()_+-=[]{}<>,./~";
        var random = new Random();
        return await Task.Run(() =>
            new string(Enumerable.Repeat(chars, 48)
                .Select(s => s[random.Next(s.Length)]).ToArray()));
    }
}
