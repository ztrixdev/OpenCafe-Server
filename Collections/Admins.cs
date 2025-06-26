using System.Security.Cryptography;
using MongoDB.Driver;
using MongoDB.Bson;
using OpenCafe.Server.DBmgmt;
using OpenCafe.Server.Helpers;

namespace OpenCafe.Server.Collections;

/// <summary>
/// Admin class. Represents objects from the admins collection in the database. 
/// </summary>
public class Admin(string name, string[] roles, int? boundTo, string token)
{
    public ObjectId _id { get; set; }
    public string Name { get; set; } = name;
    public string[] Roles { get; set; } = roles;
    public int? BoundTo { get; set; } = boundTo;
    public string Token { get; set; } = token;
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
    /// A function that searches for an admin object in the database by their token. 
    /// </summary>
    /// <param name="token"></param>
    /// <param name="database">Initialized Database object</param>
    /// <returns>
    /// - null if no Admin with the provided token was found in the database
    /// - an object of the Admin class if the search completed successfully
    /// </returns>
    /// <exception cref="ArgumentNullException">If the provided token is null or empty</exception>
    public static async Task<Admin> GetAdminByToken(string token, Database database)
    {
            if (string.IsNullOrWhiteSpace(token))
                throw new ArgumentNullException(nameof(token), "Token cannot be empty.");

            string key = database.collectionEncryption["admins"]["key"];
            var adminCollection = database._database.GetCollection<Admin>("admins");

            var admins = await adminCollection.Find(_ => true).ToListAsync();

            foreach (var admin in admins)
            {
                try
                {
                    string decryptedToken = await CryptoHelper.DecryptAsync(admin.Token, key);

                    if (decryptedToken == token)
                        return admin;
                }
                catch (CryptographicException)
                {
                    continue;
                }
            }

            return null; // Not found
    }

    public static async Task<bool> CheckHead(string Token, Database database)
    {
        var admin = await GetAdminByToken(Token, database);

        if (admin == null || !admin.Roles.Contains("head"))
            return false;
        return !false;
    }

    /// <summary>
    /// Login function for admins. Check if the provided token, if encrypted, matches with any present in the database.
    /// </summary>
    /// <param name="token">Unencrypted admin token</param>
    /// <param name="database">Initialized Database object</param>
    /// <returns>
    /// - Bad Request if the token isn't provided;
    /// - Unauthorized if none of the tokens in the database match with the provided
    /// - OK with the admin object
    /// </returns>
    public static async Task<IResult> Login(LoginRequest request, Database database)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return Results.BadRequest();

        var admin = await GetAdminByToken(request.Token, database);

        return admin == null ? Results.Unauthorized() : Results.Ok(admin);
    }

    /// <summary>
    /// Register function. Allows a head admin to "hire" new admins with a default "general" role, an auto-generated token and a custom name.
    /// </summary>
    /// <param name="request">Refer to RegisterRequest docs</param>
    /// <param name="database">Initialized Database object</param>
    /// <returns>
    /// - Bad Request if one of the fields is not provided;
    /// - Unauthorized if the token provided doesn't belong to a head admin;
    /// - OK
    /// </returns>
    public static async Task<IResult> Register(RegisterRequest request, Database database)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest("One or more of the request fields is not specified!");

        var admin = await GetAdminByToken(request.Token, database);

        if (admin != null && await CheckHead(admin.Token, database))
        {
            var newToken = await CryptoHelper.EncryptAsync(await new Admins().GenTokenAsync(), database.collectionEncryption["admins"]["key"]);
            var newAdmin = new Admin(name: request.Name, roles: ["general"], token: newToken, boundTo: null);
            await database._database.GetCollection<Admin>("admins").InsertOneAsync(newAdmin);
            return Results.Ok(newAdmin);
        }

        return Results.Unauthorized();
    }

    /// <summary>
    /// Name changing function. Allows one admin to change the other admin's name. 
    /// </summary>
    /// <param name="request">Refer to ChangeNameRequest docs</param>
    /// <param name="database">Initialized Database object</param>
    /// <returns>
    /// - Bad Request if one of the fields is not provided;
    /// - Unauthorized if Token1 doesn't belong to a head admin or Token! != Token2;
    /// - Not Found if one of the tokens doesn't belong to anybody;
    /// - OK.
    /// </returns>
    public static async Task<IResult> ChangeName(ChangeNameRequest request, Database database)
    {
        if (string.IsNullOrWhiteSpace(request.Token1) || string.IsNullOrWhiteSpace(request.Token2) || string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest("One or more of the request fields is not specified!");

        var adminObjects = new Admin[] {
            await GetAdminByToken(request.Token1, database),
            await GetAdminByToken(request.Token2, database)
        };

        if (adminObjects.Any(x => x == null)) return Results.NotFound("Token1 or Token2 holder was not found in the database.");

        if (
        adminObjects[0].Token == adminObjects[1].Token
         ||  await CheckHead(adminObjects[0].Token, database) 
         || (adminObjects[0].Roles.Contains("general") && adminObjects[1].Roles.Contains("sprvsr"))
         )
        {
            var filter = new BsonDocument("Token", adminObjects[1].Token);
            var update = new BsonDocument("$set", new BsonDocument("Name", request.Name));
            var res = await database._database.GetCollection<Admin>("admins").UpdateOneAsync(filter, update);
            return Results.Ok(res);
        }

        return Results.Unauthorized();
    }

    /// <summary>
    /// Deleting function. Allows a head admin to delete an admin.
    /// </summary>
    /// <param name="request">Refer to DeleteRequest docs</param>
    /// <param name="database">Initialized Database object</param>
    /// <returns>
    /// - Bad Request if one of the fields is not provided;
    /// - Unauthorized if Token1 doesn't belong to a head admin;
    /// - Not Found if one of the tokens doesn't belong to anybody;
    /// - OK.
    /// </returns>
    public static async Task<IResult> Delete(DeleteRequest request, Database database)
    {
        if (string.IsNullOrWhiteSpace(request.Token1) || string.IsNullOrWhiteSpace(request.Token2))
            return Results.BadRequest("One or more of the request fields is not specified!");

        var adminObjects = new Admin[] {
            await GetAdminByToken(request.Token1, database),
            await GetAdminByToken(request.Token2, database)
        };

        if (adminObjects.Any(x => x == null)) return Results.NotFound("Token1 or Token2 holder was not found in the database.");

        if (await CheckHead(adminObjects[0].Token, database)  || adminObjects[0].Roles.Contains("general") && adminObjects[1].Roles.Contains("sprvsr"))
        {
            await database._database.GetCollection<Admin>("admins").DeleteOneAsync(admin => admin.Token == adminObjects[1].Token);
            return Results.Ok($"{adminObjects[1].Name} was deleted successfuly.");
        }

        return Results.Unauthorized();
    }

    public static async Task<IResult> GetAll(GetAllRequest request, Database database)
    {
        if (string.IsNullOrEmpty(request.Token)) return Results.BadRequest("Specify a head token before requesting.");

        var admin = await GetAdminByToken(request.Token, database);

        if (admin == null) return Results.NotFound();
        if (!await CheckHead(admin.Token, database)) return Results.Unauthorized();

        var adminObjects = await database._database.GetCollection<Admin>("admins").Find(_ => true).ToListAsync();
        foreach (var adminObject in adminObjects)
        {
            adminObject.Token = await CryptoHelper.DecryptAsync(adminObject.Token, database.collectionEncryption["admins"]["key"]);
        }

        return Results.Ok(adminObjects);
    }
}