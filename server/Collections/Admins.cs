using System.Security.Cryptography;
using MongoDB.Driver;
using server.DBmgmt;
using System.Net.Http;
using MongoDB.Bson;
using server.Helpers;

namespace server.Collections;

/// <summary>
/// Admin class. Represents objects from the admins collection in the database. 
/// </summary>
public class Admin
{
    public ObjectId _id { get; set; }
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
    public record LoginRequest(string Token); 
    public record RegisterRequest(string Token, string Name);
    
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

    /// <summary>
    /// Login function for admins. Check if the provided token, if encrypted, matches with any present in the database.
    /// </summary>
    /// <param name="token">Unencrypted admin token</param>
    /// <param name="database">Initialized Database object</param>
    /// <returns>OK or Unauthorized</returns>
    public static async Task<IResult> Login(string token, Database database)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Results.BadRequest();
        }
        
        await Console.Out.WriteLineAsync(token);
        var encryptedToken = await CryptoHelper.EncryptAsync(token, database.collectionEncryption["admins"]["key"], database.collectionEncryption["admins"]["iv"]);
        
        var adminCollection = database._database.GetCollection<Admin>("admins");
        var admin = await adminCollection.Find((Admin admin) => admin.Token == encryptedToken).FirstOrDefaultAsync();
        
        return admin == null ? Results.Unauthorized() : Results.Ok(admin);
    }

    /// <summary>
    /// Register function. Allows a head admin to "hire" new admins with a default "general" role, an auto-generated token and a custom name.
    /// </summary>
    /// <param name="token">Head admin token</param>
    /// <param name="name">Custom name for the new Admin</param>
    /// <param name="database">Initialized Database object</param>
    /// <returns>OK or Unauthorized</returns>
    public static async Task<IResult> Register(string token, string name, Database database)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(name))
        {
            return Results.BadRequest();
        }
        
        var encryptedToken = await CryptoHelper.EncryptAsync(token, database.collectionEncryption["admins"]["key"], database.collectionEncryption["admins"]["iv"]);
        
        var adminCollection = database._database.GetCollection<Admin>("admins");
        var admin = await adminCollection.FindAsync((Admin admin) => admin.Token == encryptedToken);

        var fod = await admin.FirstOrDefaultAsync();
        if (fod != null && fod.Role == "head")
        {
            var newToken = await CryptoHelper.EncryptAsync(await CryptoHelper.RandomBase64Async(), database.collectionEncryption["admins"]["key"], database.collectionEncryption["admins"]["iv"]);
            var newAdmin = new Admin(name, "general", newToken);
            await adminCollection.InsertOneAsync(newAdmin);
            return Results.Ok(newAdmin);
        }

        return Results.Unauthorized();
    }
}