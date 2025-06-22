using System.Data.Common;
using MongoDB.Bson;
using MongoDB.Driver;
using OpenCafe.Server.DBmgmt;

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
    ObjectId[] images
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
    public ObjectId[] Images { get; set; } = images;
}

public class Dishes
{
    public static async Task<Dish> GetDishByID(int ID, Database database)
    {
        return await database._database.GetCollection<Dish>("dishes").Find(dish => dish.DishID == ID).FirstOrDefaultAsync();
    }
}
