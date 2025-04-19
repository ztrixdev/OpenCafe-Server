using System.Security.Cryptography;
using MongoDB.Driver;
using server.DBmgmt;
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
    /// <summary>
    /// Login request body.
    /// </summary>
    /// <param name="Token">Admin token to log in with</param>
    public record LoginRequest(string Token); 

    /// <summary>
    /// Registration request body.
    /// </summary>
    /// <param name="Token">Head or supervisor token</param>
    /// <param name="Name">The new admin's name</param>
    public record RegisterRequest(string Token, string Name);

    /// <summary>
    /// Name changing request body. 
    /// </summary>
    /// <param name="Token1">Must belong to a head or a supervisor (also works if == Token2)</param>
    /// <param name="Token2">The victim of name-changing</param>
    /// <param name="Name">The new name</param>
    public record ChangeNameRequest(string Token1, string Token2, string Name);

    /// <summary>
    /// Delete request body.
    /// </summary>
    /// <param name="Token1">Must belong to a head admin</param>
    /// <param name="Token2">The one who gets deleted</param>
    public record DeleteRequest(string Token1, string Token2);

    /// <summary>
    /// Get all request body.
    /// </summary>
    /// <param name="Token">Must belong to a head admin</param>
    public record GetAllRequest(string Token);

    /// <summary>
    /// Asynchronously generates a 48 character Admin token.
    /// </summary>
    /// <returns>Look up bro</returns>
    public async Task<string> GenTokenAsync()
    {
        var tokenChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789–_.~";
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
    /// <returns>
    /// - Bad Request if the token isn't provided; 
    /// - Unauthorized if none of the tokens in the database match with the provided.
    /// - OK.
    /// </returns>
    public static async Task<IResult> Login(LoginRequest req, Database database)
    {
        if (string.IsNullOrWhiteSpace(req.Token))
            return Results.BadRequest();

        var encryptedToken = await CryptoHelper.EncryptAsync(req.Token, database.collectionEncryption["admins"]["key"], database.collectionEncryption["admins"]["iv"]);
        
        var adminCollection = database._database.GetCollection<Admin>("admins");
        var admin = await adminCollection.Find((Admin admin) => admin.Token == encryptedToken).FirstOrDefaultAsync();
        
        return admin == null ? Results.Unauthorized() : Results.Ok(admin);
    }

    /// <summary>
    /// Register function. Allows a head admin to "hire" new admins with a default "general" role, an auto-generated token and a custom name.
    /// </summary>
    /// <param name="req">Refer to RegisterRequest docs</param>
    /// <param name="database">Initialized Database object</param>
    /// <returns>
    /// - Bad Request if one of the fields is not provided;
    /// - Unauthorized if the token provided doesn't belong to a head admin;
    /// - OK
    /// </returns>
    public static async Task<IResult> Register(RegisterRequest req, Database database)
    {
        if (string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrWhiteSpace(req.Name))
            return Results.BadRequest("One or more of the request fields is not specified!");
        
        var encryptedToken = await CryptoHelper.EncryptAsync(req.Token, database.collectionEncryption["admins"]["key"], database.collectionEncryption["admins"]["iv"]);
        
        var adminCollection = database._database.GetCollection<Admin>("admins");
        var admin = await adminCollection.FindAsync((Admin admin) => admin.Token == encryptedToken);

        var fod = await admin.FirstOrDefaultAsync();
        if (fod != null && fod.Role == "head")
        {
            var newToken = await CryptoHelper.EncryptAsync(await new Admins().GenTokenAsync(), database.collectionEncryption["admins"]["key"], database.collectionEncryption["admins"]["iv"]);
            var newAdmin = new Admin(req.Name, "general", newToken);
            await adminCollection.InsertOneAsync(newAdmin);
            return Results.Ok(newAdmin);
        }

        return Results.Unauthorized();
    }

    /// <summary>
    /// Name changing function. Allows one admin to change the other admin's name. 
    /// </summary>
    /// <param name="req">Refer to ChangeNameRequest docs</param>
    /// <param name="database">Initialized Database object</param>
    /// <returns>
    /// - Bad Request if one of the fields is not provided;
    /// - Unauthorized if Token1 doesn't belong to a head admin or Token! != Token2;
    /// - Not Found if one of the tokens doesn't belong to anybody;
    /// - OK.
    /// </returns>
    public static async Task<IResult> ChangeName(ChangeNameRequest req, Database database)
    {
        if (string.IsNullOrWhiteSpace(req.Token1) || string.IsNullOrWhiteSpace(req.Token2) || string.IsNullOrWhiteSpace(req.Name))
            return Results.BadRequest("One or more of the request fields is not specified!");

        var adminCollection = database._database.GetCollection<Admin>("admins");
        
        var encryptedTokens = new string[] {
            await CryptoHelper.EncryptAsync(req.Token1, database.collectionEncryption["admins"]["key"], database.collectionEncryption["admins"]["iv"]),
            await CryptoHelper.EncryptAsync(req.Token2, database.collectionEncryption["admins"]["key"], database.collectionEncryption["admins"]["iv"])
        };

        var adminObjects = new Admin[] {
            await adminCollection.Find((Admin admin) => admin.Token == encryptedTokens[0]).FirstOrDefaultAsync(),
            await adminCollection.Find((Admin admin) => admin.Token == encryptedTokens[1]).FirstOrDefaultAsync()
        };

        if (adminObjects.Any(x => x == null)) return Results.NotFound("Token1 or Token2 holder was not found in the database.");

        if (adminObjects[0].Token == adminObjects[1].Token|| adminObjects[0].Role == "head")
        {
            var filter = new BsonDocument("Token", adminObjects[1].Token);
            var update = new BsonDocument("$set", new BsonDocument("Name", req.Name));
            var res = await adminCollection.UpdateOneAsync(filter, update);
            return Results.Ok(res);
        }

        return Results.Unauthorized();
    }

    /// <summary>
    /// Deleting function. Allows a head admin to delete an admin.
    /// </summary>
    /// <param name="req">Refer to DeleteRequest docs</param>
    /// <param name="database">Initialized Database object</param>
    /// <returns>
    /// - Bad Request if one of the fields is not provided;
    /// - Unauthorized if Token1 doesn't belong to a head admin;
    /// - Not Found if one of the tokens doesn't belong to anybody;
    /// - OK.
    /// </returns>
    public static async Task<IResult> Delete(DeleteRequest req, Database database) 
    {
        if (string.IsNullOrWhiteSpace(req.Token1) || string.IsNullOrWhiteSpace(req.Token2))
            return Results.BadRequest("One or more of the request fields is not specified!");

        var adminCollection = database._database.GetCollection<Admin>("admins");
        
        var encryptedTokens = new string[] {
            await CryptoHelper.EncryptAsync(req.Token1, database.collectionEncryption["admins"]["key"], database.collectionEncryption["admins"]["iv"]),
            await CryptoHelper.EncryptAsync(req.Token2, database.collectionEncryption["admins"]["key"], database.collectionEncryption["admins"]["iv"])
        };

        var adminObjects = new Admin[] {
            await adminCollection.Find((Admin admin) => admin.Token == encryptedTokens[0]).FirstOrDefaultAsync(),
            await adminCollection.Find((Admin admin) => admin.Token == encryptedTokens[1]).FirstOrDefaultAsync()
        };

        if (adminObjects.Any(x => x == null)) return Results.NotFound("Token1 or Token2 holder was not found in the database.");

        if (adminObjects[0].Role == "head")
        {  
            await adminCollection.DeleteOneAsync((Admin admin) => admin.Token == adminObjects[1].Token);
            return Results.Ok($"{adminObjects[1].Name} was deleted successfuly.");
        }

        return Results.Unauthorized();
    }

    public static async Task<IResult> GetAll(GetAllRequest req, Database database) 
    {
        if (string.IsNullOrEmpty(req.Token)) return Results.BadRequest("Specify a head token before requesting.");
        
        var adminCollection = database._database.GetCollection<Admin>("admins");

        var encryptedToken = await CryptoHelper.EncryptAsync(req.Token, database.collectionEncryption["admins"]["key"], database.collectionEncryption["admins"]["iv"]);
        var admin = await adminCollection.Find((Admin admin) => admin.Token == encryptedToken).FirstOrDefaultAsync();

        if (admin.Role != "head") return Results.Unauthorized();

        var adminObjects = await adminCollection.Find(_ => true).ToListAsync();
        foreach (var adminObject in adminObjects) {
            adminObject.Token = await CryptoHelper.DecryptAsync(adminObject.Token, database.collectionEncryption["admins"]["key"], database.collectionEncryption["admins"]["iv"]);
        }

        return Results.Ok(adminObjects);
    }
}