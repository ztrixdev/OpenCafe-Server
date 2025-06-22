using MongoDB.Driver;
using MongoDB.Bson;
using OpenCafe.Server.DBmgmt;

namespace OpenCafe.Server.Collections;

// Addresses and admins moved to Points
public class Instance
(
    bool isBackup,
    string[] cultures, string logo,
    Dictionary<string, string> name,
    Dictionary<string, string> description,
    string[] pics
)
{
    public ObjectId? _id { get; set; }
    public bool IsBackup { get; set; } = isBackup;
    public string[]? Cultures { get; set; } = cultures;
    public string? Logo { get; set; } = logo;
    public Dictionary<string, string>? Name { get; set; } = name;
    public Dictionary<string, string>? Description { get; set; } = description;
    public string[]? Pics { get; set; } = pics;
}

public class InstanceMgmt
{
    public record FlashRequest(string Token, Instance Instance);
    public record IdRequest(string Token, ObjectId _id);

    public static async Task<Instance> Load(Database database)
    {
        return await database._database.GetCollection<Instance>("instances").Find(instance => !instance.IsBackup).FirstOrDefaultAsync();
    }

    public static async Task<IResult> Flash(FlashRequest request, Database database)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || request.Instance == null)
            return Results.BadRequest("One or more of the required fields is not provided!");

        if (request.Instance.IsBackup == true)
            return Results.BadRequest("Do not try to flash a backup as a stream instance configuration!");

        if (!await Admins.CheckHead(request.Token, database))
            return Results.Unauthorized();

        var instanceCollection = database._database.GetCollection<Instance>("Instances");
        await instanceCollection.DeleteManyAsync((Instance instance) => instance.IsBackup == false);
        await instanceCollection.InsertOneAsync(request.Instance);

        return Results.Ok();
    }

    public static async Task<IResult> Restore(IdRequest request, Database database)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request._id.Timestamp.ToString()))
            return Results.BadRequest("One or more of the required fields is not provided!");

        if (!await Admins.CheckHead(request.Token, database))
            return Results.Unauthorized();

        var instanceCollection = database._database.GetCollection<Instance>("Instances");
        var bckp = await instanceCollection.Find((Instance instance) => instance._id == request._id).FirstOrDefaultAsync();
        bckp.IsBackup = false;

        var frq = new FlashRequest(request.Token, bckp);
        var flash = await Flash(frq, database);

        return flash;
    }

    public static async Task<IResult> Delete(IdRequest request, Database database)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request._id.Timestamp.ToString()))
            return Results.BadRequest("One or more of the required fields is not provided!");

        if (!await Admins.CheckHead(request.Token, database))
            return Results.Unauthorized();

        var instanceCollection = database._database.GetCollection<Instance>("Instances");
        var bckp = await instanceCollection.Find((Instance instance) => instance._id == request._id).FirstOrDefaultAsync();
        if (bckp == null)
            return Results.NotFound();

        if (bckp.IsBackup == false)
            return Results.BadRequest("Cannot delete the stream configuration!");

        var deletion = await instanceCollection.DeleteOneAsync((Instance instance) => instance._id == request._id);
        return Results.Ok(deletion);
    }

    public static async Task<IResult> Copy(IdRequest request, Database database)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request._id.Timestamp.ToString()))
            return Results.BadRequest("One or more of the required fields is not provided!");

        if (!await Admins.CheckHead(request.Token, database))
            return Results.Unauthorized();

        var instanceCollection = database._database.GetCollection<Instance>("Instances");
        var bckp = await instanceCollection.Find((Instance instance) => instance._id == request._id).FirstOrDefaultAsync();
        bckp.IsBackup = true;

        await instanceCollection.InsertOneAsync(bckp);
        return Results.Ok();
    }
}
