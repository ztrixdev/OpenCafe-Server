using MongoDB.Driver;
using server.DBmgmt;
using MongoDB.Bson;

namespace server.Collections;

/// <summary>
/// Admin class. Represents objects from the admins collection in the database. 
/// </summary>
public class Instance
{
    public ObjectId? _id { get; set; }
    public bool IsBackup { get; set; }
    public string[]? Cultures { get; set; } // Languages used to localize the menu and other aspects
    public string? Logo { get; set; }
    public Dictionary<string, string>? Name { get; set; }
    public Dictionary<string, string>? Description { get; set; }
    public Dictionary<string, string[]>? Adresses { get; set; }
    public string[]? Pics { get; set; }
    public string[]? Admins { get; set; }

    public Instance(
        bool isBackup,
        string[] cultures, string logo,
        Dictionary<string, string> name,
        Dictionary<string, string> description,
        Dictionary<string, string[]> adresses,
        string[] pics,
        string[] admins
    )
    {
        IsBackup = isBackup;
        Cultures = cultures;
        Logo = logo;
        Name = name;
        Description = description;
        Adresses = adresses;
        Pics = pics;
        Admins = admins;
    }
}

public class InstanceMgmt
{
    public record FlashRequest(string Token, Instance Instance);
    public record IdRequest(string Token, ObjectId _id);

    public static async Task<IResult> Flash(FlashRequest request, Database database)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || request.Instance == null)
            return Results.BadRequest("One or more of the required fields is not provided!");

        if (request.Instance.IsBackup == true)
            return Results.BadRequest("Do not try to flash a backup as a stream instance configuration!");

        if (await Admins.CheckHead(request.Token, database) == Results.Unauthorized())
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

        if (await Admins.CheckHead(request.Token, database) == Results.Unauthorized())
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

        if (await Admins.CheckHead(request.Token, database) == Results.Unauthorized())
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

        if (await Admins.CheckHead(request.Token, database) == Results.Unauthorized())
            return Results.Unauthorized();

        var instanceCollection = database._database.GetCollection<Instance>("Instances");
        var bckp = await instanceCollection.Find((Instance instance) => instance._id == request._id).FirstOrDefaultAsync();
        bckp.IsBackup = true;

        await instanceCollection.InsertOneAsync(bckp);
        return Results.Ok();
    }
}
