
using MongoDB.Bson;
using MongoDB.Driver;
using OpenCafe.Server.DBmgmt;

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
}
