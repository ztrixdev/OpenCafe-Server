using MongoDB.Bson;
using MongoDB.Driver;
using server.DBmgmt;

namespace server.Collections;

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
    public record RaiseRequest(string Raiser, string Title, string Contacts, string Description);

    public record ModifyRequest(string Token, string Action, int IssueID);

    public static async Task<IResult> Raise(RaiseRequest request, Database database)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Raiser) || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Contacts) || string.IsNullOrWhiteSpace(request.Description))
            return Results.BadRequest("One or more of the request fields is not provided!");

        var admin = await Admins.GetAdminByToken(request.Raiser, database);
        if (admin == null || !admin.Roles.Contains("sprvsr"))
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
        var issuesCollection = database._database.GetCollection<Issue>("issues");
        await issuesCollection.InsertOneAsync(issue);

        var newIssue = await issuesCollection.Find(issue => issue.IssueID == iid).FirstOrDefaultAsync();
        return Results.Ok(newIssue);
    }

    public static async Task<IResult> GetAll(Admins.GetAllRequest request, Database database)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Token))
            return Results.BadRequest("One or more of the request fields is not provided!");

        var admin = await Admins.GetAdminByToken(request.Token, database);
        if (admin == null || !admin.Roles.Contains("general"))
            return Results.Unauthorized();

        var issuesCollection = database._database.GetCollection<Issue>("issues");
        var issues = issuesCollection.Find(_ => true).ToListAsync();
        return Results.Ok(issues);
    }

    public static async Task<IResult> Modify(ModifyRequest request, Database database)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Action) || request.IssueID == -1)
            return Results.BadRequest("One or more of the request fields is not provided!");

        var issuesCollection = database._database.GetCollection<Issue>("issues");
        var issue = await issuesCollection.Find(issue => issue.IssueID == request.IssueID).FirstOrDefaultAsync();
        if (issue == null)
            return Results.NotFound("An issue with the provided IssueID was not found in the database!");

        var admin = await Admins.GetAdminByToken(request.Token, database);
        if
        (
            admin == null
            || (!(admin.Roles.Contains("general") && admin.BoundTo == issue.Point)
            && !(admin.Roles.Contains("sprvsr") && issue.Raiser == admin.Token))
        )
            return Results.Unauthorized();

        BsonDocument update, filter = new("IssueID", issue.IssueID);
        switch (request.Action)
        {
            case "close":
                update = new("$set", new BsonDocument("IsActive", false));
                break;
            case "+monitor":
                update = new("$set", new BsonDocument("IsMonitored", true));
                break;
            case "-monitor":
                update = new("$set", new BsonDocument("IsMonitored", false));
                break;
            default:
                return Results.BadRequest("Cannot perform any operation other than: \"close\", \"+monitor\", \"-monitor\"!");
        }

        await issuesCollection.UpdateOneAsync(filter, update);
        return Results.Ok();
    }
}
