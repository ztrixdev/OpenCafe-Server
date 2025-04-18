using MongoDB.Bson;
using server.DBmgmt;
using server.Helpers;

namespace server.Collections;

/// <summary>
/// Image class. Represents objects from the images collection of the database.
/// </summary>
public class Image {
    public ObjectId _id { get; set; }
    public string Filename { get; set; }
    public string Author { get; set; }
    public string Alt { get; set; }
    public DateTime Uploaded { get; set; }

    public Image(string filename, string author, string alt) 
    {
        Filename = filename;
        Author = author;
        Alt = alt;
        Uploaded = DateTime.Now;
    }
}

public class Images
{
    /// <summary>
    /// Upload and sign request body.
    /// </summary>
    /// <param name="Image">The image file that is going to be uploaded.</param>
    /// <param name="Author">The author's token (must be an admin).</param>
    /// <param name="Alt">HTML's alt parameter's synonym.</param>
    public record UploadRequest(IFormFile Image, string Author, string Alt);

    /// <summary>
    /// Filename generative func. Generates a template-based name for an uploaded image for later use. 
    /// The template looks like this (currently):
    /// first 10 chars of Alt_meow_last 6 digits of the current UnixTimeSeconds.extension
    /// </summary>
    /// <param name="ext">File extension</param>
    /// <param name="alt">Alt for the image</param>
    /// <returns></returns>
    public static string GenFilename(string ext, string alt) 
    {
        var currentTime = DateTimeOffset.Now.ToUnixTimeSeconds();
        return $"{alt.Substring(0,10)}_meow_{currentTime.ToString().Substring(currentTime.ToString().Length - 6)}{ext}";
    }

    /// <summary>
    /// Image uploading function. Allows admins to upload images to be publically accessible and write info about the image to the DB.
    /// </summary>
    /// <param name="req">Refer to UploadRequest docs</param>
    /// <param name="database">Initialized Database object</param>
    /// <returns>
    /// - Bad Request if one of the fields is null.
    /// - Unauthorized if the Author is not an admin.
    /// - Unprocessable Entity if the Image doesn't have the right extension or if it's bigger than 5 MB
    /// - OK
    /// </returns>
    public static async Task<IResult> Upload(UploadRequest req, Database database)
    {
        if (req.Image == null || string.IsNullOrEmpty(req.Author) || string.IsNullOrEmpty(req.Alt))
            return Results.BadRequest();

        if (req.Image.Length > 5_000_000) 
            return Results.UnprocessableEntity();

        var loginReq = new Admins.LoginRequest(req.Author);
        var isAdmin = await Admins.Login(loginReq, database);
        if (isAdmin == Results.Unauthorized())
            return Results.Unauthorized();

        var fileext = Path.GetExtension(req.Image.FileName);
        if (fileext != ".jpeg" && fileext != ".jpg" && fileext != ".png") 
            return Results.UnprocessableEntity();

        var imgFileName = GenFilename(fileext, req.Alt);
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenCafe/fs/img");
        
        using (var stream = new FileStream(Path.Combine(dir, imgFileName), FileMode.Create)) 
        {
            await req.Image.CopyToAsync(stream);
        }

        var imageCollection = database._database.GetCollection<Image>("images");
        var encryptedToken = await CryptoHelper.EncryptAsync(req.Author, database.collectionEncryption["admins"]["key"], database.collectionEncryption["admins"]["iv"]);
        await imageCollection.InsertOneAsync(new Image(filename: imgFileName, author: encryptedToken, alt: req.Alt));

        return Results.Ok("fs/img/" + imgFileName);
    }
    
}

