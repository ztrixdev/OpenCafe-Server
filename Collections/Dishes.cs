using System.Data.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Bson;
using MongoDB.Driver;
using OpenCafe.Server.DBmgmt;
using OpenCafe.Server.Helpers;

namespace OpenCafe.Server.Collections;

public class Dish
(
    int dishID,
    int price,
    bool isOnSale,
    int oldPrice,
    string nameSI,
    string descSI,
    Dictionary<string, int> nutriProfile,
    string[] images
)
{
    public ObjectId Id { get; set; }
    public int DishID { get; set; } = dishID;
    public int Price { get; set; } = price;
    public bool IsOnSale { get; set; } = isOnSale;
    public int OldPrice { get; set; } = oldPrice;
    public string NameSI { get; set; } = nameSI;
    public string DescriptionSI { get; set; } = descSI;
    public Dictionary<string, int> NutriProfile { get; set; } = nutriProfile;
    public string[] Images { get; set; } = images;
}

public class Dishes
{
    /// <summary>
    /// Represents a request to create a new dish.
    /// </summary>
    /// <param name="Token">The authentication token for the admin creating the dish.</param>
    /// <param name="Name">The name of the dish.</param>
    /// <param name="Description">The description of the dish.</param>
    /// <param name="Price">The price of the dish.</param>
    /// <param name="NutriProfile">A dictionary representing the nutritional profile of the dish.</param>
    /// <param name="Images">An array of image identifiers associated with the dish.</param>
    public record NewRequest(string Token, string Name, string Description, int Price, Dictionary<string, int> NutriProfile, string[] Images);

    /// <summary>
    /// Represents a request to retrieve or delete a dish by its ID.
    /// </summary>
    /// <param name="DID">The unique identifier of the dish.</param>
    /// <param name="Token">The authentication token for the admin performing the action.</param>
    public record DIDTRequest(int DID, string Token);

    /// <summary>
    /// Represents a request to update a dish's details.
    /// </summary>
    /// <param name="DID">The unique identifier of the dish to be updated.</param>
    /// <param name="Token">The authentication token for the admin performing the update.</param>
    /// <param name="Updates">A dictionary containing the fields to be updated and their new values.</param>
    public record UpdateRequest(int DID, string Token, Dictionary<string, string> Updates);

    public static async Task<Dish> GetDishByID(int ID, Database database)
    {
        return await database._database.GetCollection<Dish>(nameof(Database.Collections.dishes)).Find(dish => dish.DishID == ID).FirstOrDefaultAsync();
    }

    /// <summary>
    /// Creates a new dish in the database.
    /// </summary>
    /// <param name="request">Refer to NewRequest docs.</param>
    /// <param name="database">Initialized Database object.</param>
    /// <returns>
    /// - Bad Request if any required fields are missing or invalid;
    /// - Unauthorized if the admin does not have the necessary permissions;
    /// - OK if the dish is successfully created and returns the created Dish object.
    /// </returns>
    public static async Task<IResult> New(NewRequest request, Database database)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Token)
        || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Description)
        || request.NutriProfile == null || !request.NutriProfile.ContainsKey("weight") || !request.NutriProfile.ContainsKey("calories")
        || !request.NutriProfile.ContainsKey("proteins") || !request.NutriProfile.ContainsKey("fats") || !request.NutriProfile.ContainsKey("carbohydrates"))
            return Results.BadRequest("One or more of the request fields is not specified!");

        var admin = await Admins.GetAdminByToken(request.Token, database);
        if (admin == null || admin.Roles.Contains(nameof(Admins.Roles.general)))
            return Results.Unauthorized();

        List<string> dishImgObjIdArr = new();
        foreach (var imageID in request.Images)
        {
            var successfulParsing = ObjectId.TryParse(imageID, out var objId);
            if (!successfulParsing)
                return Results.BadRequest("Cannot parse one of the images' ObjectId!");

            var image = await Images.GetOne(objId, database);
            if (image is not NotFoundResult)
                dishImgObjIdArr.Add(objId.ToString());
        }

        var rnd = new Random();
        var newDishID = rnd.Next();
        while (await GetDishByID(newDishID, database) != null)
            newDishID = rnd.Next();
        string nameSI = Strings.GenSI(whatFor: nameof(Strings.AllowedWhatFor.DISH), originalID: newDishID, whereAt: nameof(Strings.AllowedWhereAt.NAME)),
        descSI = Strings.GenSI(whatFor: nameof(Strings.AllowedWhatFor.DISH), originalID: newDishID, whereAt: nameof(Strings.AllowedWhereAt.DESCRIPTION));

        var instance = await InstanceMgmt.Load(database);

        var DCstrName = new String(culture: instance.Cultures[0], content: request.Name,
        si: nameSI, outdated: false);
        var DCstrDesc = new String(culture: instance.Cultures[0], content: request.Description,
    si: descSI, outdated: false);

        await Strings.InsertNew(@string: DCstrName, database);
        await Strings.InsertNew(@string: DCstrDesc, database);

        var dishesCollection = database._database.GetCollection<Dish>(nameof(Database.Collections.dishes));
        var dish = new Dish(newDishID, request.Price, false, request.Price, nameSI, descSI, request.NutriProfile, [.. dishImgObjIdArr]);
        await dishesCollection.InsertOneAsync(dish);

        dish = await dishesCollection.Find(dish => dish.DishID == newDishID).FirstOrDefaultAsync();
        return Results.Ok(dish);
    }


    /// <summary>
    /// Updates the details of an existing dish.
    /// </summary>
    /// <param name="request">Refer to UpdateRequest docs.</param>
    /// <param name="database">Initialized Database object.</param>
    /// <returns>
    /// - Bad Request if any required fields are missing or invalid;
    /// - Unauthorized if the admin does not have the necessary permissions;
    /// - Not Found if the dish with the specified ID does not exist;
    /// - OK if the dish is successfully updated and returns the updated Dish object.
    /// </returns>
    public static async Task<IResult> Update(UpdateRequest request, Database database)
    {
         if (string.IsNullOrWhiteSpace(request.Token) || request.Updates == null || request.DID == -1)
            return Results.BadRequest("One or more of the request fields is not specified!");

        var admin = await Admins.GetAdminByToken(request.Token, database);
        if (admin == null || !admin.Roles.Contains(nameof(Admins.Roles.general)))
            return Results.Unauthorized();

        var dishesCollection = database._database.GetCollection<Dish>(nameof(Database.Collections.dishes));

        var dish = await dishesCollection.Find(dish => dish.DishID == request.DID).FirstOrDefaultAsync();
        if (dish == null) return Results.NotFound("Cannot find a dish with such an ID");

        BsonDocument filter = new(nameof(Dish.DishID), request.DID);
        BsonDocument update;
        foreach (var key in request.Updates.Keys)
        {
            switch (key)
            {
                case "+image":
                    if (await Images.GetOne(ObjectId.Parse(request.Updates[key]), database) is OkObjectResult)
                    {
                        update = new(BsonOperations.Push, new BsonDocument(nameof(Database.Collections.customers), request.Updates[key]));
                        await dishesCollection.UpdateOneAsync(filter, update);
                    }
                    else return Results.Conflict("Cannot upload a non-existent image!");
                    break;
                case "-image":
                    if (dish.Images == null)
                        return Results.Conflict("Cannot remove an image from a dish with no images!");
                    if (
                        await Images.GetOne(ObjectId.Parse(request.Updates[key]), database) is OkObjectResult
                        && dish.Images.Contains(request.Updates[key])
                    )
                    {
                        update = new(BsonOperations.Pull, new BsonDocument(nameof(Database.Collections.customers), request.Updates[key]));
                        await dishesCollection.UpdateOneAsync(filter, update);
                    }
                    break;
                case "name":
                case "description":
                    {
                        var modifSI = key == "name" ? dish.NameSI : dish.DescriptionSI;
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
                case "weight":
                case "calories":
                case "proteins":
                case "fats":
                case "carbohydrates":
                    {
                        var canBeParsed = Int32.TryParse(request.Updates[key], out var newValue);
                        if (!canBeParsed)
                            return Results.BadRequest("Cannot parse a new nutritional value into a valid integer!");

                        update = new(BsonOperations.Set, new BsonDocument(key, newValue));
                        await dishesCollection.UpdateOneAsync(filter, update);

                        break;
                    }
                
                default:
                    return Results.BadRequest("Cannot perform any of the provided updates");
            }
        }

        return Results.Ok(await dishesCollection.Find(dish => dish.DishID == request.DID).FirstOrDefaultAsync());
    }

    /// <summary>
    /// Deletes a dish from the database by its unique identifier.
    /// </summary>
    /// <param name="request">Refer to DIDTRequest docs.</param>
    /// <param name="database">Initialized Database object.</param>
    /// <returns>
    /// - Bad Request if any required fields are missing or invalid;
    /// - Unauthorized if the admin does not have the necessary permissions;
    /// - Not Found if the dish with the specified ID does not exist;
    /// - OK if the dish is successfully deleted.
    /// </returns>
    public static async Task<IResult> Delete(DIDTRequest request, Database database)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Token) || request.DID == -1)
            return Results.BadRequest("One or more of the request fields is not specified!");

        var admin = await Admins.GetAdminByToken(request.Token, database);
        if (admin == null || admin.Roles.Contains(nameof(Admins.Roles.general)))
            return Results.Unauthorized();

        var dish = await GetDishByID(request.DID, database);
        if (dish == null)
            return Results.NotFound("There is no such dish to delete.");

        BsonDocument filter = new("DishID", request.DID);
        var deletionResult = await database._database.GetCollection<Dish>(nameof(Database.Collections.dishes)).DeleteOneAsync(filter);

        return Results.Ok(deletionResult);
    }
}


