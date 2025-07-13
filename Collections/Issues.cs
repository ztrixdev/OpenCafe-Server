using MongoDB.Bson;
using MongoDB.Driver;
using OpenCafe.Server.DBmgmt;
using OpenCafe.Server.Helpers;

namespace OpenCafe.Server.Collections;

public class Issue
(
    int issueID,
    bool isActive,
    bool isMonitored,
    string raiser,
    int? point,
    DateTime whenRaised,
    string title,
    string contacts,
    string description
)
{
    public ObjectId _id { get; set; }
    public int IssueID { get; set; }
    public bool IsActive { get; set; } = isActive;
    public bool IsMonitored { get; set; } = isMonitored;
    public string Raiser { get; set; } = raiser;
    public int? Point { get; set; } = point;
    public DateTime WhenRaised { get; set; } = whenRaised;
    public string Title { get; set; } = title;
    public string Contacts { get; set; } = contacts;
    public string Description { get; set; } = description;
}

public class Issues()
{
    /// <summary>
    /// Represents a request to raise a new issue.
    /// </summary>
    /// <param name="Raiser">The identifier of the person raising the issue.</param>
    /// <param name="Title">The title of the issue.</param>
    /// <param name="Contacts">Contact information related to the issue.</param>
    /// <param name="Description">A detailed description of the issue.</param>
    public record RaiseRequest(string Raiser, string Title, string Contacts, string Description);

    /// <summary>
    /// Represents a request to modify an existing issue.
    /// </summary>
    /// <param name="Token">The authentication token for the admin performing the modification.</param>
    /// <param name="Action">The action to perform on the issue (e.g., "close", "+monitor", "-monitor").</param>
    /// <param name="IssueID">The unique identifier of the issue to be modified.</param>
    public record ModifyRequest(string Token, string Action, int IssueID);


    /// <summary>
    /// Raises a new issue in the system.
    /// </summary>
    /// <param name="request">Refer to RaiseRequest docs.</param>
    /// <param name="database">Initialized Database object.</param>
    /// <returns>
    /// - Bad Request if any required fields are missing or invalid;
    /// - Unauthorized if the admin does not have the necessary permissions;
    /// - OK if the issue is successfully raised and returns the created Issue object.
    /// </returns>
    public static async Task<IResult> Raise(RaiseRequest request, Database database)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Raiser) || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Contacts) || string.IsNullOrWhiteSpace(request.Description))
            return Results.BadRequest("One or more of the request fields is not provided!");

        var admin = await Admins.GetAdminByToken(request.Raiser, database);
        if (admin == null || !admin.Roles.Contains(nameof(Admins.Roles.sprvsr)))
            return Results.Unauthorized();

        var iid = new Random().Next();
        var issue = new Issue
        (
            issueID: iid,
            isActive: true, isMonitored: false,
            raiser: admin.Token, point: admin.BoundTo,
            title: request.Title, contacts: request.Contacts, description: request.Description,
            whenRaised: DateTime.Now
        );
        var issuesCollection = database._database.GetCollection<Issue>(nameof(Database.Collections.issues));
        await issuesCollection.InsertOneAsync(issue);

        var newIssue = await issuesCollection.Find(issue => issue.IssueID == iid).FirstOrDefaultAsync();
        return Results.Ok(newIssue);
    }

    /// <summary>
    /// Retrieves all issues from the database.
    /// </summary>
    /// <param name="request">Refer to Admins.GetAllRequest docs.</param>
    /// <param name="database">Initialized Database object.</param>
    /// <returns>
    /// - Bad Request if the token is not provided;
    /// - Unauthorized if the admin does not have the necessary permissions;
    /// - OK if the issues are successfully retrieved.
    /// </returns>
    public static async Task<IResult> GetAll(Admins.GetAllRequest request, Database database)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Token))
            return Results.BadRequest("One or more of the request fields is not provided!");

        var admin = await Admins.GetAdminByToken(request.Token, database);
        if (admin == null || !admin.Roles.Contains(nameof(Admins.Roles.general)))
            return Results.Unauthorized();

        var issuesCollection = database._database.GetCollection<Issue>(nameof(Database.Collections.issues));
        var issues = issuesCollection.Find(_ => true).ToListAsync();
        return Results.Ok(issues);
    }

    /// <summary>
    /// Modifies the status of an existing issue.
    /// </summary>
    /// <param name="request">Refer to ModifyRequest docs.</param>
    /// <param name="database">Initialized Database object.</param>
    /// <returns>
    /// - Bad Request if any required fields are missing or invalid;
    /// - Unauthorized if the admin does not have the necessary permissions;
    /// - Not Found if the issue with the specified IssueID does not exist;
    /// - OK if the issue is successfully modified.
    /// </returns>
    public static async Task<IResult> Modify(ModifyRequest request, Database database)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Action) || request.IssueID == -1)
            return Results.BadRequest("One or more of the request fields is not provided!");

        var issuesCollection = database._database.GetCollection<Issue>(nameof(Database.Collections.issues));
        var issue = await issuesCollection.Find(issue => issue.IssueID == request.IssueID).FirstOrDefaultAsync();
        if (issue == null)
            return Results.NotFound("An issue with the provided IssueID was not found in the database!");

        var admin = await Admins.GetAdminByToken(request.Token, database);
        if
        (
            admin == null
            || (!(admin.Roles.Contains(nameof(Admins.Roles.general)) && admin.BoundTo == issue.Point)
            && !(admin.Roles.Contains(nameof(Admins.Roles.sprvsr)) && issue.Raiser == admin.Token))
        )
            return Results.Unauthorized();

        BsonDocument update, filter = new("IssueID", issue.IssueID);
        switch (request.Action)
        {
            case "close":
                update = new(BsonOperations.Set, new BsonDocument("IsActive", false));
                break;
            case "+monitor":
                update = new(BsonOperations.Set, new BsonDocument("IsMonitored", true));
                break;
            case "-monitor":
                update = new(BsonOperations.Set, new BsonDocument("IsMonitored", false));
                break;
            default:
                return Results.BadRequest("Cannot perform any operation other than: \"close\", \"+monitor\", \"-monitor\"!");
        }

        await issuesCollection.UpdateOneAsync(filter, update);
        return Results.Ok();
    }
}
