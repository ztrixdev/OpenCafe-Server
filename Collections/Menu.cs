using MongoDB.Bson;
using MongoDB.Driver;
using OpenCafe.Server.DBmgmt;

namespace OpenCafe.Server.Collections;

public class Menu
(
    int menuID,
    string nameSI,
    string descSI,
    int[] dishes
)
{
    public ObjectId Id { get; set; }
    public int MenuID { get; set; } = menuID;
    public string NameSI { get; set; } = nameSI;
    public string DescriptionSI { get; set; } = descSI;
    public int[] Dishes { get; set; } = dishes;
}

public class Menus
{
    public record CreateRequest(string Token, string Name, string Description, int FirstDIsh);
    public record MIDTRequest(string Token, int ID);

    public static async Task<IResult> Create(CreateRequest request, Database database)
    {
        if (request == null
        || string.IsNullOrWhiteSpace(request.Token)
        || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Description)
        || request.FirstDIsh == -1)
            return Results.BadRequest("One or more of the request fields is not specified!");

        var admin = await Admins.GetAdminByToken(request.Token, database);
        if (admin == null || !admin.Roles.Contains("general"))
            return Results.Unauthorized();

        if (await Dishes.GetDishByID(request.FirstDIsh, database) == null)
            return Results.BadRequest("The dish that was supposed to be the first dish of the menu doesn't exist!");

        var instance = await InstanceMgmt.Load(database);
        var newMenuID = new Random().Next();
        string nameSI = Strings.GenSI(whatFor: "menu", originalID: newMenuID, whereAt: "name"),
        descSI = Strings.GenSI(whatFor: "menu", originalID: newMenuID, whereAt: "description");

        var DCstrName = new String(culture: instance.Cultures[0], content: request.Name,
        si: nameSI, outdated: false);
        var DCstrDesc = new String(culture: instance.Cultures[0], content: request.Description,
       si: descSI, outdated: false);

        await Strings.InsertNew(@string: DCstrName, database);
        await Strings.InsertNew(@string: DCstrDesc, database);

        var menuCollection = database._database.GetCollection<Menu>("menu");
        await menuCollection.InsertOneAsync(new Menu(newMenuID, nameSI, descSI, [request.FirstDIsh]));

        return Results.Created();
    }
}
