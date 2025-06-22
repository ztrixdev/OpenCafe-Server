using MongoDB.Driver;
using OpenCafe.Server.DBmgmt;
using MongoDB.Bson;
using Microsoft.AspNetCore.Mvc;
using OpenCafe.Server.Helpers;

namespace OpenCafe.Server.Collections;

public class Point
(
    int pointID,
    string[]? supervisors,
    string? address,
    string[]? pics,
    int[]? unavaliable,
    int[]? reviews,
    int[]? activeissues
)
{
    public ObjectId Id { get; set; }
    public int PointID { get; set; } = pointID;
    public string[]? Supervisors { get; set; } = supervisors;
    public string? Address { get; set; } = address;
    public string[]? Pics { get; set; } = pics;
    public int[]? Unavaliable { get; set; } = unavaliable;
    public int[]? Reviews { get; set; } = reviews;
    public int[]? ActiveIssues { get; set; } = activeissues;
}

public class Points
{
    public record AddRequest(string Address, string Token);
    public record TwoAdminRequest(int PID, string Token1, string Token2, string Action);
    public record UpdateRequest(int PID, string Token, Dictionary<string, string> Updates);
    public record PIDTRequest(int PID, string Token);
    public record PIDRequest(int PID);

    /// <summary>
    /// Creates a new point in the system. Only accessible by a head admin.
    /// </summary>
    /// <param name="request">Refer to <see cref="AddRequest"/> for required fields.</param>
    /// <param name="database">Initialized Database object.</param>
    /// <returns>
    /// - Bad Request if the address or token is not provided;
    /// - Unauthorized if the requester is not a head admin;
    /// - OK.
    /// </returns>
    public static async Task<IResult> New(AddRequest request, Database database)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Address) || string.IsNullOrWhiteSpace(request.Token))
            return Results.BadRequest("One or more of the request fields is not specified!");

        if (!await Admins.CheckHead(request.Token, database))
            return Results.Unauthorized();

        var newPoint = new Point(new Random().Next(), null, request.Address, null, null, null, null);

        await database._database.GetCollection<Point>("points").InsertOneAsync(newPoint);
        return Results.Ok();
    }

    /// <summary>
    /// Updates an existing point's details. Requires a general admin bound to the point.
    /// </summary>
    /// <param name="request">Refer to <see cref="UpdateRequest"/> for updatable fields.</param>
    /// <param name="database">Initialized Database object.</param>
    /// <returns>
    /// - Bad Request if required fields are missing or invalid updates are requested;
    /// - Unauthorized if the requester is not authorized to modify the point;
    /// - Not Found if the point does not exist;
    /// - Conflict if attempting to modify non-existent images;
    /// - OK.
    /// </returns>
    public static async Task<IResult> Update(UpdateRequest request, Database database)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || request.Updates == null || request.PID == -1)
            return Results.BadRequest("One or more of the request fields is not specified!");

        var admin = await Admins.GetAdminByToken(request.Token, database);
        if (admin == null || !admin.Roles.Contains("general") || admin.BoundTo != request.PID)
            return Results.Unauthorized();

        var pointsCollection = database._database.GetCollection<Point>("points");

        var point = await pointsCollection.Find((Point point) => point.PointID == request.PID).FirstOrDefaultAsync();
        if (point == null) return Results.NotFound("Cannot find a point with such an ID");

        BsonDocument filter = new("PointID", request.PID);
        BsonDocument update;
        foreach (var key in request.Updates.Keys)
        {
            switch (key)
            {
                case "address":
                    update = new("$set", new BsonDocument("Address", request.Updates[key]));
                    await pointsCollection.UpdateOneAsync(filter, update);
                    break;
                case "+pic":
                    if (await Images.GetOne(ObjectId.Parse(request.Updates[key]), database) is OkObjectResult)
                    {
                        update = new("$push", new BsonDocument("Pics", request.Updates[key]));
                        await pointsCollection.UpdateOneAsync(filter, update);
                    }
                    else return Results.Conflict("Cannot upload a non-existent image!");
                    break;
                case "-pic":
                    if (point.Pics == null)
                        return Results.Conflict("Cannot remove an image from a point with no images!");
                    if (
                        await Images.GetOne(ObjectId.Parse(request.Updates[key]), database) is OkObjectResult
                        && point.Pics.Contains(request.Updates[key])
                    )
                    {
                        update = new("$pull", new BsonDocument("Pics", request.Updates[key]));
                        await pointsCollection.UpdateOneAsync(filter, update);
                    }
                    break;
                default:
                    return Results.BadRequest("Cannot perform any of the provided updates");
            }
        }

        return Results.Ok(await pointsCollection.Find(point => point.PointID == request.PID).FirstOrDefaultAsync());
    }

    /// <summary>
    /// Deletes a point and unbinds associated admins. Only accessible by a head admin.
    /// </summary>
    /// <param name="request">Refer to <see cref="PIDTRequest"/> for required fields.</param>
    /// <param name="database">Initialized Database object.</param>
    /// <returns>
    /// - Bad Request if required fields are missing;
    /// - Unauthorized if the requester is not a head admin;
    /// - Not Found if the point does not exist;
    /// - OK.
    /// </returns>
    public static async Task<IResult> Delete(PIDTRequest request, Database database)
    {
        if (request == null || request.PID == -1 || string.IsNullOrWhiteSpace(request.Token))
            return Results.BadRequest("One or more of the request fields is not specified!");

        if (!await Admins.CheckHead(request.Token, database))
            return Results.Unauthorized();

        var pointsCollection = database._database.GetCollection<Point>("points");
        var point = await pointsCollection.Find(point => point.PointID == request.PID).FirstOrDefaultAsync();
        if (point == null)
            return Results.NotFound("Cannot find any point with the provided PointID!");

        // Unbind all admins associated with the point that is being removed from the system
        var adminsCollection = database._database.GetCollection<Admin>("admins");
        var filter = new BsonDocument("BoundTo", request.PID);
        var update = new BsonDocument("$set", new BsonDocument("BoundTo", -1));
        await adminsCollection.UpdateManyAsync(filter, update);

        // Remove the point
        var point_filter = new BsonDocument("PointID", request.PID);
        await pointsCollection.DeleteOneAsync(point_filter);

        return Results.Ok();
    }

    /// <summary>
    /// Handles admin binding to a point.
    /// </summary>
    /// <param name="request">Refer to <see cref="TwoAdminRequest"/> for required fields.</param>
    /// <param name="database">Initialized Database object.</param>
    /// <returns>
    /// - Bad Request if required fields are missing;
    /// - Not Found if either admin does not exist;
    /// - Unauthorized if the requesting admin lacks permissions;
    /// - OK.
    /// </returns>
    public static async Task<IResult> PointAdminActions(TwoAdminRequest request, Database database)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Token1) || string.IsNullOrWhiteSpace(request.Token2) || string.IsNullOrWhiteSpace(request.Action))
            return Results.BadRequest("One or more of the request fields is not specified!");

        var adminObjects = new Admin[] {
            await Admins.GetAdminByToken(request.Token1, database),
            await Admins.GetAdminByToken(request.Token2, database)
        };

        if (adminObjects.Any(x => x == null)) return Results.NotFound("Token1 or Token2 holder was not found in the database.");

        var pointsCollection = database._database.GetCollection<Point>("points");
        var adminsCollection = database._database.GetCollection<Admin>("admins");

        if
        (
            (adminObjects[0].Roles.Contains("general") && adminObjects[0].BoundTo == request.PID && adminObjects[1].Roles.Contains("sprvsr"))
            || (await Admins.CheckHead(adminObjects[0].Token, database) && (adminObjects[1].Roles.Contains("general") || adminObjects[1].Roles.Contains("sprvsr")))
        )
        {
            string BsonAction;
            int UpdateID;
            switch (request.Action)
            {
                case "hire":
                    BsonAction = "$push"; UpdateID = request.PID;
                    break;
                case "fire":
                    BsonAction = "$pull"; UpdateID = -1;
                    break;
                default:
                    return Results.BadRequest("Cannot perform any operation other than hiring or firing!");
            }

            var point_filter = new BsonDocument("PointID", request.PID);
            var encryptedToken = await CryptoHelper.EncryptAsync(request.Token2, database.collectionEncryption["admins"]["key"], database.collectionEncryption["admins"]["iv"]);
            var point_update = new BsonDocument(BsonAction, new BsonDocument("Supervisors", encryptedToken));
            await pointsCollection.UpdateOneAsync(point_filter, point_update);

            var admin_filter = new BsonDocument("Token", request.Token2);
            var admin_update = new BsonDocument("$set", new BsonDocument("BoundTo", UpdateID));
            await adminsCollection.UpdateOneAsync(admin_filter, admin_update);

            return Results.Ok();
        }

        return Results.Unauthorized();
    }

    public static async Task<IResult> LoadByPID(int PID, Database database)
    {
        if (PID == -1) return Results.BadRequest("Point with a PointID -1 cannot exist.");

        var point = await database._database.GetCollection<Point>("points").Find(point => point.PointID == PID).FirstOrDefaultAsync();

        if (point == null) return Results.NotFound();
        else return Results.Ok(point);
    }
    
    public static async Task<IResult> LoadAll(Database database)
    {
        var points = await database._database.GetCollection<Point>("points").Find(_ => true).ToListAsync();
        return Results.Ok(points);
    }
}
