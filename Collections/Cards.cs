using System.Diagnostics;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using NCalc;
using NCalc.Exceptions;
using server.DBmgmt;
using server.Helpers;
using server.Logging;

namespace server.Collections;

public class Card
{
    [BsonId]
    public ObjectId Id { get; set; }

    // Customer's InternalID
    public long OwnerIID { get; set; }
    // The card's id (Customer's CardID)
    [BsonElement("ID")]
    public string ID { get; set; }
    public long Balance { get; set; }
    public long[]? Orders { get; set; }

    public Card(long ownerIID, string id, long balance, long[]? orders)
    {
        OwnerIID = ownerIID;
        ID = id;
        Balance = balance;
        Orders = orders;
    }
}

public class Cards
{
    public record OIIDRequest(long OwnerIID);
    public record IDRequest(long ID);
    public record RetractRequest(long ID, string Token, int ToRetract);

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
        var encID = await CryptoHelper.EncryptAsync(newID.ToString(), database.collectionEncryption["cards"]["key"], database.collectionEncryption["cards"]["iv"]);
        await cardCollection.InsertOneAsync(new Card(ownerIID: request.OwnerIID, id: encID, balance: 0, orders: null));

        var filter = new BsonDocument("InternalID", request.OwnerIID);
        var update = new BsonDocument("$set", new BsonDocument("Card", encID));
        var res = await database._database.GetCollection<Customer>("customers").UpdateOneAsync(filter, update);

        return Results.Ok(res);
    }

    public static async Task<IResult> Verify(IDRequest request, Database database)
    {
        if (request == null || request.ID == 0)
            return Results.BadRequest("No card ID was provided.");

        var encID = await CryptoHelper.EncryptAsync(request.ID.ToString(), database.collectionEncryption["cards"]["key"], database.collectionEncryption["cards"]["iv"]);

        var cardCollection = database._database.GetCollection<Card>("cards");
        var card = await cardCollection.Find((Card card) => card.ID == encID).FirstOrDefaultAsync();

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

    public static async Task<IResult> Get(Customers.EmailPasswordRequest request, Database database)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Results.BadRequest("One or more of the request fields is not provided.");

        var loginRes = await Customers.Login(request, database);
        if (loginRes is OkObjectResult)
        {
            var customer = await Customers.GetCustomerBy(new KeyValuePair<string, string>("email", request.Email), database);

            var cardCollection = database._database.GetCollection<Card>("cards");
            var card = await cardCollection.Find((Card card) => card.OwnerIID == customer.InternalID).FirstOrDefaultAsync();
            if (card == null) return Results.NotFound("No card has been registered for this user.");

            var decodedCardID = await CryptoHelper.DecryptAsync(card.ID, database.collectionEncryption["cards"]["key"], database.collectionEncryption["cards"]["iv"]);
            var parseID = Int64.TryParse(decodedCardID, out var decID);
            if (!parseID) return Results.Conflict();

            return await Verify(new IDRequest(decID), database);
        }

        return Results.Unauthorized();
    }

    public static async Task<IResult> Retract(RetractRequest request, Database database)
    {
        if (request == null || request.ToRetract == 0 || request.ID == 0 )
            return Results.BadRequest("One or more of the request fields is not provided.");

        var encodedCardID = await CryptoHelper.EncryptAsync(request.ID.ToString(), database.collectionEncryption["cards"]["key"], database.collectionEncryption["cards"]["iv"]);
        
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
        var filter = new BsonDocument("ID", encodedCardID);
        var update = new BsonDocument("$set", balance - request.ToRetract);
        var res = cardCollection.UpdateOneAsync(filter, update);
        return Results.Ok(res);
     }
}



