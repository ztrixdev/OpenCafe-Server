
using MongoDB.Bson;
using MongoDB.Driver;
using OpenCafe.Server.DBmgmt;
using OpenCafe.Server.Helpers;
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

    public enum AllowedWhatFor
    {
        MENU, DISH, CATEGORY, OTHER
    }

    public enum AllowedWhereAt
    {
        NAME, DESCRIPTION
    }
    
    /// <summary>
    /// Generates a string identifier (SI) based on the provided parameters.
    /// </summary>
    /// <param name="whatFor">The type of entity the SI is for (e.g., "MENU", "DISH").</param>
    /// <param name="originalID">The original ID of the entity.</param>
    /// <param name="whereAt">The context of the string (e.g., "NAME", "DESCRIPTION").</param>
    /// <returns>The generated string identifier (SI).</returns>
    /// <exception cref="ArgumentException">Thrown if the parameters are not valid.</exception>
    public static string GenSI(
        string whatFor, int originalID, string whereAt
    )
    {
        string[] allowedWhatFor = [.. Enum.GetNames<AllowedWhatFor>()];
        string[] allowedWhereAt = [.. Enum.GetNames<AllowedWhereAt>()];
        if (!allowedWhatFor.Contains(whatFor.ToUpper()) || !allowedWhereAt.Contains(whereAt.ToUpper()))
            throw new ArgumentException($"Cannot create an SI for something that is not in the allowed list. Allowed list: {allowedWhatFor.ToString} + {allowedWhereAt.ToString}");

        return $"{whatFor.ToUpper()}%{originalID}%{whereAt.ToUpper()}";
    }

    /// <summary>
    /// Retrieves a list of strings by their string identifier (SI).
    /// </summary>
    /// <param name="SI">The string identifier to search for.</param>
    /// <param name="database">Initialized Database object.</param>
    /// <returns>A list of strings matching the provided SI.</returns>
    public static async Task<List<String>> GetBySI(string SI, Database database)
    {
        var stringsCollection = database._database.GetCollection<String>(nameof(Database.Collections.strings));
        return await stringsCollection.Find(str => str.SI == SI).ToListAsync();
    }


    /// <summary>
    /// Validates a string identifier (SI) to ensure it meets basic criteria.
    /// </summary>
    /// <param name="SI">The string identifier to validate.</param>
    /// <returns>True if the SI is valid; otherwise, false.</returns>
    public static bool ValidateSI(string SI)
    {
        // TODO: Extend for advanced String Identifier validation.
        if (!SI.Contains('%')) return false;
        return true;
    }

    /// <summary>
    /// Inserts a new string into the database.
    /// </summary>
    /// <param name="string">The string object to insert.</param>
    /// <param name="database">Initialized Database object.</param>
    /// <returns>A list of strings matching the inserted string's SI.</returns>
    /// <exception cref="ArgumentException">Thrown if the string details are incomplete.</exception>
    public static async Task<List<String>> InsertNew(String @string, Database database)
    {
        if (string.IsNullOrWhiteSpace(@string.Culture) || string.IsNullOrWhiteSpace(@string.Content) || !ValidateSI(@string.SI))
            throw new ArgumentException("Cannot insert a non-detailed string!");

        var stringsCollection = database._database.GetCollection<String>(nameof(Database.Collections.strings));

        await stringsCollection.InsertOneAsync(@string);
        var sistr = await GetBySI(@string.SI, database);

        return sistr;
    }

    /// <summary>
    /// Updates the content of an existing string in the database.
    /// </summary>
    /// <param name="SI">The string identifier of the string to update.</param>
    /// <param name="culture">The culture of the string to update.</param>
    /// <param name="newContent">The new content for the string.</param>
    /// <param name="database">Initialized Database object.</param>
    /// <returns>A list of strings matching the updated string's SI.</returns>
    /// <exception cref="ArgumentException">Thrown if any required parameters are missing.</exception>
    /// <exception cref="NotSupportedException">Thrown if the string to update does not exist.</exception>
    public static async Task<List<String>> Update(string SI, string culture, string newContent, Database database)
    {
        if (string.IsNullOrWhiteSpace(SI) || string.IsNullOrWhiteSpace(culture) || string.IsNullOrWhiteSpace(newContent))
            throw new ArgumentException("An SI, a culture and new content are required to execute this operation.");

        var _ = await GetBySI(SI, database) ?? throw new NotSupportedException("Cannot update a non-existing string.");

        var stringsCollection = database._database.GetCollection<String>(nameof(Database.Collections.strings));

        BsonDocument filter = new(nameof(String.Culture), culture), update = new(BsonOperations.Set, new BsonDocument("Content", newContent));
        await stringsCollection.UpdateOneAsync(filter, update);

        filter = new(nameof(String.Culture), new BsonDocument(BsonOperations.NotEqual, culture));
        update = new(BsonOperations.Set, new BsonDocument("Outdated", true));
        await stringsCollection.UpdateManyAsync(filter, update);

        return await GetBySI(SI, database);
    }
}
