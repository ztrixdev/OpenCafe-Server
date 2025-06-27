using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Driver;
using OpenCafe.Server.DBmgmt;
using OpenCafe.Server.Helpers;
using System.Text;
using XSystem.Security.Cryptography;

namespace OpenCafe.Server.Collections;

public class Card(long ownerIID, string id, string hash, long balance, long[]? orders)
{
    [BsonId]
    public ObjectId Id { get; set; }

    // Customer's InternalID
    public long OwnerIID { get; set; } = ownerIID;
    // The card's id (Customer's CardID)
    [BsonElement("ID")]
    public string ID { get; set; } = id;
    public string Hash { get; set; } = hash;
    public long Balance { get; set; } = balance;
    public long[]? Orders { get; set; } = orders;
}

public class Cards
{
    /// <summary>
    /// Represents a request to issue a new card for a customer identified by their internal ID.
    /// </summary>
    /// <param name="OwnerIID">The internal ID of the customer for whom the card is being issued.</param>
    public record OIIDRequest(long OwnerIID);

    /// <summary>
    /// Represents a request to verify a card using its ID.
    /// </summary>
    /// <param name="ID">The ID of the card to be verified.</param>
    public record IDRequest(long ID);

    /// <summary>
    /// Represents a request to retract a specified amount from a card's balance.
    /// </summary>
    /// <param name="ID">The ID of the card from which points are to be retracted.</param>
    /// <param name="Token">The authentication token for the admin performing the retraction.</param>
    /// <param name="ToRetract">The amount of points to retract from the card's balance.</param>
    public record RetractRequest(long ID, string Token, int ToRetract);


    /// <summary>
    /// Issues a new card for a cutomer. 
    /// </summary>
    /// <param name="request">Refer to OIIDRequest docs</param>
    /// <param name="database">Initialized Database object</param>
    /// <returns>
    /// - Bad Request if there was no owner's ID to issue a card for;
    /// - Not Found if the provided owner's ID doesn't belong to anyone;
    /// - Conflict if the owner already has a card;
    /// - OK.
    /// </returns>
    public static async Task<IResult> Issue(OIIDRequest request, Database database)
    {
        if (request == null || request.OwnerIID == 0)
            return Results.BadRequest("No Owner's ID to issue a card for.");

        var customer = await Customers.GetCustomerBy(new KeyValuePair<string, string>("iid", $"{request.OwnerIID}"), database);
        if (customer == null)
            return Results.NotFound("Cannot issue a card for an unexistent customer.");
        if (customer.Card != null)
            return Results.Conflict("Cannot issue a card since it already exists.");

        var cardCollection = database._database.GetCollection<Card>("cards");
        var newID = new Random().NextInt64();
        var encID = await CryptoHelper.EncryptAsync(newID.ToString(), database.collectionEncryption["cards"]["key"]);
        var source = ASCIIEncoding.ASCII.GetBytes(newID.ToString());
        var hash = new MD5CryptoServiceProvider().ComputeHash(source);
        await cardCollection.InsertOneAsync(new Card(ownerIID: request.OwnerIID, id: encID, hash: hash.ToString(), balance: 0, orders: null));

        var filter = new BsonDocument("InternalID", request.OwnerIID);
        var update = new BsonDocument("$set", new BsonDocument("Card", encID));
        var res = await database._database.GetCollection<Customer>("customers").UpdateOneAsync(filter, update);

        return Results.Ok(res);
    }

    /// <summary>
    /// Card verification function. 
    /// </summary>
    /// <param name="request">Refer to IDRequest docs</param>
    /// <param name="database">Initialized Database object</param>
    /// <returns
    /// - OK with a dictionary like this:                 
    /// {"id", request.ID},
    /// { "valid", true },
    /// { "balance", card.Balance}
    /// - Not Found with a dictionary like this:
    /// {"id", request.ID},
    /// { "valid", false }
    /// </returns>
    public static async Task<IResult> Verify(IDRequest request, Database database)
    {
        if (request == null || request.ID == 0)
            return Results.BadRequest("No card ID was provided.");

        var source = ASCIIEncoding.ASCII.GetBytes(request.ID.ToString());
        var hashedID = new MD5CryptoServiceProvider().ComputeHash(source);
        var cardCollection = database._database.GetCollection<Card>("cards");
        var card = await cardCollection.Find(card => card.Hash == hashedID.ToString()).FirstOrDefaultAsync();

        Dictionary<string, object> response;
        if (card != null)
        {
            response = new Dictionary<string, object>()
            {
                {"id", request.ID},
                { "valid", true },
                { "balance", card.Balance}
            };
            return Results.Ok(response);
        }

        response = new Dictionary<string, object>()
        {
            {"id", request.ID},
            { "valid", false },
        };
        return Results.NotFound(response);
    }

    /// <summary>
    /// Retrieves the card associated with a customer after verifying their login credentials.
    /// </summary>
    /// <param name="request">Refer to Customers.EmailPasswordRequest docs</param>
    /// <param name="database">Initialized Database object</param>
    /// <returns>
    /// - Bad Request if the email or password is not provided;
    /// - Unauthorized if the login credentials are invalid;
    /// - Not Found if no card is registered for the user;
    /// - OK if the card is successfully retrieved and verified.
    /// </returns>
    public static async Task<IResult> Get(Customers.EmailPasswordRequest request, Database database)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Results.BadRequest("One or more of the request fields is not provided.");

        var loginRes = await Customers.Login(request, database);
        if (loginRes is OkObjectResult)
        {
            var customer = await Customers.GetCustomerBy(new KeyValuePair<string, string>("email", request.Email), database);

            var cardCollection = database._database.GetCollection<Card>("cards");
            var card = await cardCollection.Find(card => card.OwnerIID == customer.InternalID).FirstOrDefaultAsync();
            if (card == null) return Results.NotFound("No card has been registered for this user.");

            var decodedCardID = await CryptoHelper.DecryptAsync(card.ID, database.collectionEncryption["cards"]["key"]);
            var parseID = Int64.TryParse(decodedCardID, out var decID);
            if (!parseID) return Results.Conflict();

            return await Verify(new IDRequest(decID), database);
        }

        return Results.Unauthorized();
    }

    /// <summary>
    /// Retracts a specified amount from the card balance after verifying admin credentials and card validity.
    /// </summary>
    /// <param name="request">Refer to RetractRequest docs</param>
    /// <param name="database">Initialized Database object</param>
    /// <returns>
    /// - Bad Request if any of the request fields are not provided;
    /// - Unauthorized if the admin login credentials are invalid;
    /// - Not Found if the card is not valid;
    /// - Conflict if the amount to retract exceeds the current balance;
    /// - OK if the balance is successfully updated.
    /// </returns>
    public static async Task<IResult> Retract(RetractRequest request, Database database)
    {
        if (request == null || request.ToRetract == 0 || request.ID == 0 )
            return Results.BadRequest("One or more of the request fields is not provided.");

        var adminLoginResult = await Admins.Login(new Admins.LoginRequest(request.Token), database);
        if (adminLoginResult is not OkObjectResult)
            return adminLoginResult;

        Dictionary<string, object> cardData;
        var verifyResult = await Verify(new IDRequest(request.ID), database);
        if (verifyResult is OkObjectResult result1)
            cardData = result1.Value as Dictionary<string, object>;
        else return verifyResult;

        var CanCheckBalance = Int32.TryParse(cardData["balance"].ToString(), out int balance);
        if (!CanCheckBalance)
            return Results.Conflict("Cannot check the card's balance.");
        if (request.ToRetract > balance)
            return Results.Conflict("Cannot retract more points than the amount present on balance.");

        var cardCollection = database._database.GetCollection<Card>("cards");

        var source = ASCIIEncoding.ASCII.GetBytes(request.ID.ToString());
        var hashedID = new MD5CryptoServiceProvider().ComputeHash(source);
        var filter = new BsonDocument("Hash", hashedID);
        
        var update = new BsonDocument("$set", new BsonDocument("Balance", balance - request.ToRetract));
        var res = cardCollection.UpdateOneAsync(filter, update);
        return Results.Ok(res);
     }
}



