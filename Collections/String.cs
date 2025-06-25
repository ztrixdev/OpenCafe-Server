
using MongoDB.Bson;
using MongoDB.Driver;
using OpenCafe.Server.DBmgmt;
using Parlot.Fluent;

namespace OpenCafe.Server.Collections;

public class String
(
    string culture,
    string content,
    string si,
    bool outdated
)
{
    public ObjectId Id { get; set; }
    public string Culture { get; set; } = culture;
    public string Content { get; set; } = content;
    public string SI { get; set; } = si;
    public bool Outdated { get; set; } = outdated;
}

public class Strings
{
    public static string GenSI(
        string whatFor, int originalID, string whereAt
    )
    {
        string[] allowedWhatFor = ["MENU", "DISH", "CATEGORY", "OTHER"];
        string[] allowedWhereAt = ["NAME", "DESCRIPTION"];
        if (!allowedWhatFor.Contains(whatFor.ToUpper()) || !allowedWhereAt.Contains(whereAt.ToUpper()))
            throw new ArgumentException($"Cannot create an SI for something that is not in the allowed list. Allowed list: {allowedWhatFor.ToString} + {allowedWhereAt.ToString}");

        return $"{whatFor.ToUpper()}%{originalID}%{whereAt.ToUpper()}";
    }

    public static async Task<List<String>> GetBySI(string SI, Database database)
    {
        var stringsCollection = database._database.GetCollection<String>("strings");
        return await stringsCollection.Find(str => str.SI == SI).ToListAsync();
    }

    public static bool ValidateSI(string SI)
    {
        // TODO: Extend for advanced String Identifier validation.
        if (!SI.Contains('%')) return false;
        return true;
    }

    public static async Task<List<String>> InsertNew(String @string, Database database)
    {
        if (string.IsNullOrWhiteSpace(@string.Culture) || string.IsNullOrWhiteSpace(@string.Content) || !ValidateSI(@string.SI))
            throw new ArgumentException("Cannot insert a non-detailed string!");

        var stringsCollection = database._database.GetCollection<String>("strings");

        await stringsCollection.InsertOneAsync(@string);
        var sistr = await GetBySI(@string.SI, database);

        return sistr;
    }

    public static async Task<List<String>> Update(string SI, string culture, string newContent, Database database)
    {
        if (string.IsNullOrWhiteSpace(SI) || string.IsNullOrWhiteSpace(culture) || string.IsNullOrWhiteSpace(newContent))
            throw new ArgumentException("An SI, a culture and new content are required to execute this operation.");

        var _ = await GetBySI(SI, database) ?? throw new NotSupportedException("Cannot update a non-existing string.");

        var stringsCollection = database._database.GetCollection<String>("strings");

        BsonDocument filter = new("Culture", culture), update = new("$set", new BsonDocument("Content", newContent));
        await stringsCollection.UpdateOneAsync(filter, update);

        filter = new("Culture", new BsonDocument("$ne", culture));
        update = new("$set", new BsonDocument("Outdated", true));
        await stringsCollection.UpdateManyAsync(filter, update);

        return await GetBySI(SI, database);
    }
}
