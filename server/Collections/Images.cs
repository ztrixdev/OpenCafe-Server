using MongoDB.Bson;
using server.DBmgmt;

namespace server.Collections;

/// <summary>
/// Image class. Represents objects from the images collection of the database.
/// </summary>
public class Image {
    public ObjectId _id { get; set; }
    public string? Filename { get; set; }
    public string? Author { get; set; }
    public string? Alt { get; set; }
    public DateTime Uploaded { get; set; }
}

public class Images
{
    /// <summary>
    /// Upload and sign request body.
    /// </summary>
    /// <param name="Image">The image file that is going to be uploaded.</param>
    /// <param name="Author">The author's token (must be an admin).</param>
    /// <param name="Alt">HTML's alt parameter's synonym.</param>
    public record UploadAndSignRequest(IFormFile Image, string Author, string Alt);

    /*
    public async Task<IResult> UploadAndSign(UploadAndSignRequest req, Database database)
    {
        TODO
        return Results.Ok();
    }
    */
}

