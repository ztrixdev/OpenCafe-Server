using Microsoft.AspNetCore.Identity.Data;
using MongoDB.Bson;
using MongoDB.Driver;
using OpenCafe.Server.DBmgmt;
using Parlot.Fluent;

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
    public record UpdateRequest(int MID, string Token, Dictionary<string, string> Updates);

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

    public static async Task<IResult> Update(UpdateRequest request, Database database)
    {
        if (string.IsNullOrWhiteSpace(request.Token) || request.Updates == null || request.MID == -1)
            return Results.BadRequest("One or more of the request fields is not specified!");

        var admin = await Admins.GetAdminByToken(request.Token, database);
        if (admin == null || !admin.Roles.Contains("general"))
            return Results.Unauthorized();

        var menuCollection = database._database.GetCollection<Menu>("menus");

        var menu = await menuCollection.Find(menu => menu.MenuID == request.MID).FirstOrDefaultAsync();
        if (menu == null) return Results.NotFound("Cannot find a menu with such an ID");

        BsonDocument filter = new("MenuID", request.MID);
        BsonDocument update;
        foreach (var key in request.Updates.Keys)
        {
            switch (key)
            {
                case "+dish":
                case "-dish":
                    {
                        var operation = key == "+dish" ? "$push" : "$pull";
                        var parseId = Int32.TryParse(request.Updates[key], out var dishID);

                        if (!parseId)
                            return Results.BadRequest($"The provided dish ID ({request.Updates[key]}) is not a valid integer!");

                        var dish = await Dishes.GetDishByID(dishID, database);
                        if (dish == null)
                            return Results.NotFound($"A dish with the provided ID wasn't found in the database! Dish ID: {dishID}");

                        bool dishExistsInMenu = menu.Dishes.Contains(dishID);

                        if (operation == "$push" && dishExistsInMenu)
                            return Results.Conflict($"The dish with ID={dishID} is already present in the menu!");

                        if (operation == "$pull" && !dishExistsInMenu)
                            return Results.Conflict($"The dish with ID={dishID} is not present in the menu!");

                        update = new(operation, new BsonDocument("Dishes", dishID));
                        await menuCollection.UpdateOneAsync(filter, update);
                        break;
                    }
                case "name":
                case "description":
                    {
                        var modifSI = key == "name" ? menu.NameSI : menu.DescriptionSI;
                        var instance = await InstanceMgmt.Load(database);

                        if (instance == null)
                            return Results.Conflict("Ask the head admin to set up the instance first.");

                        try
                        {
                            await Strings.Update(modifSI, instance.Cultures[0], request.Updates[key], database);
                        }
                        catch (Exception ex)
                        {
                            return Results.Conflict(ex.Message);
                        }

                        break;
                    }
                default:
                    return Results.BadRequest("Cannot perform any of the provided updates");
            }
        }

        return Results.Ok(await menuCollection.Find(menu => menu.MenuID == request.MID).FirstOrDefaultAsync());
    }

    public static async Task<IResult> Delete(MIDTRequest request, Database database)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Token) || request.ID == -1)
            return Results.BadRequest("One or more of the request fields is not specified!");

        var admin = await Admins.GetAdminByToken(request.Token, database);
        if (admin == null || !admin.Roles.Contains("general"))
            return Results.Unauthorized();

        var menuCollection = database._database.GetCollection<Menu>("menus");

        var menu = await menuCollection.Find(menu => menu.MenuID == request.ID).FirstOrDefaultAsync();
        if (menu == null)
            return Results.NotFound("Cannot delete a menu that doesn't even exist!");

        var removal = await menuCollection.DeleteOneAsync(menu => menu.MenuID == request.ID);
        return Results.Ok(removal);
    }
}
