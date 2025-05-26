using MongoDB.Bson;
using MongoDB.Driver;
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
    /// Upload request body.
    /// </summary>
    /// <param name="Image">The image file that is going to be uploaded.</param>
    /// <param name="Author">The author's token (must be an admin).</param>
    /// <param name="Alt">HTML's alt parameter's synonym.</param>
    public record UploadRequest(IFormFile Image, string Author, string Alt);
    
    /// <summary>
    /// Delete request body.
    /// </summary>
    /// <param name="ID">The image's ID (Mongo ObjectId)</param>
    /// <param name="Token">An admin token</param>
    public record DeleteRequest(ObjectId ID, string Token);

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
        if (req.Image == null || string.IsNullOrWhiteSpace(req.Author) || string.IsNullOrWhiteSpace(req.Alt))
            return Results.BadRequest("One or more of the request fields is not specified!");

        if (req.Image.Length > 8_000_000) 
            return Results.UnprocessableEntity("Uploading images bigger then 8 MB in size is not supported.");

        var isAdmin = await Admins.Login(new Admins.LoginRequest(req.Author), database);
        if (isAdmin == Results.Unauthorized())
            return isAdmin;

        var fileext = Path.GetExtension(req.Image.FileName);
        if (fileext != ".jpeg" && fileext != ".jpg" && fileext != ".png") 
            return Results.UnprocessableEntity("We only support .jpeg, .jpg and .png images.");

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
    
    /// <summary>
    /// Image deleting function. Allows an admin to delete an image from the database and from the filesystem with it's ID.
    /// </summary>
    /// <param name="req">Refer to DeleteRequest docs</param>
    /// <param name="database">Initialized Database object</param>
    /// <returns>
    /// - Bad Request if one of the request fields is not specified.
    /// - Unauthorized if the token provided doesn't belong to an admin.
    /// - Not Found if the image with the provided ID wasn't found in the DB.
    /// - OK.
    /// </returns>
    public static async Task<IResult> Delete(DeleteRequest req, Database database) 
    {
        if (string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrWhiteSpace(req.ID.ToString())) 
            return Results.BadRequest("One or more of the request fields is not specified!");
        
        var isAdmin = await Admins.Login(new Admins.LoginRequest(req.Token), database);
        if (isAdmin == Results.Unauthorized()) 
            return isAdmin;

        var imageCollection = database._database.GetCollection<Image>("images");
        var image = await imageCollection.Find((Image img) => img._id == req.ID).FirstOrDefaultAsync();
        if (image == null)
            return Results.NotFound("An image with the specified ID was not found in the database!");
        
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenCafe/fs/img");
        await Task.Run(() => File.Delete(Path.Combine(dir, image.Filename)));

        var oper = await imageCollection.DeleteOneAsync(new BsonDocument("_id", image._id));
        return Results.Ok(oper);
    }

    public static async Task<IResult> GetAll(Database database) 
    {
        var imageCollection = database._database.GetCollection<Image>("images");
        var images = await imageCollection.Find(_ => true).ToListAsync();
        
        return Results.Ok(images);
    }

    public static async Task<IResult> GetOne(ObjectId _id, Database database) 
    {
        var imageCollection = database._database.GetCollection<Image>("images");
        var image = await imageCollection.Find((Image img) => img._id == _id).FirstOrDefaultAsync();
        
        return Results.Ok(image);
    }
}

