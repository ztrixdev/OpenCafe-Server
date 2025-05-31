using System.Diagnostics;
using System.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Driver;
using NCalc;
using NCalc.Exceptions;
using server.DBmgmt;

namespace server.Collections;

public class Card
{
    public ObjectId ObjectId { get; set; }
    // Customer's InternalID
    public long OwnerIID { get; set; }
    // The card's id (Customer's CardID)
    public long ID { get; set; }
    public long Balance { get; set; }
    public long[]? Orders { get; set; }

    public Card(long ownerIID, long id, long balance, long[]? orders)
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

    public static async Task<long> CardIDOperations(string operation, long ID, Database database)
    {
        if (operation != "decode" && operation != "encode")
            throw new ArgumentException("Cannot perform any operations other than encoding and decoding.");

        AsyncExpression expression = new($"{ID}{database.collectionEncryption["cards"][operation]}");
        var result = await expression.EvaluateAsync();
        if (result != null && Int64.TryParse(result.ToString(), out var value))
            return value;
        else
            throw new NCalcEvaluationException("Could not complete the card number encoding");
    }

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
        newID = await CardIDOperations("encode", newID, database);
        await cardCollection.InsertOneAsync(new Card(ownerIID: request.OwnerIID, id: newID, balance: 0, orders: null));

        var filter = new BsonDocument("InternalID", request.OwnerIID);
        var update = new BsonDocument("$set", new BsonDocument("Card", newID));
        var res = await database._database.GetCollection<Admin>("customers").UpdateOneAsync(filter, update);

        return Results.Ok(res);
    }

    public static async Task<IResult> Verify(IDRequest request, Database database)
    {
        if (request == null || request.ID == 0)
            return Results.BadRequest("No card ID was provided.");

        var encodedID = await CardIDOperations("encode", request.ID, database);

        var cardCollection = database._database.GetCollection<Card>("cards");
        var card = await cardCollection.Find((Card card) => card.ID == encodedID).FirstOrDefaultAsync();

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
}
