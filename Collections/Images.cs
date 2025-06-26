using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using OpenCafe.Server.DBmgmt;
using OpenCafe.Server.Helpers;

namespace OpenCafe.Server.Collections;

/// <summary>
/// Image class. Represents objects from the images collection of the database.
/// </summary>
public class Image(string filename, string author, string alt)
{
    public ObjectId _id { get; set; }
    public string Filename { get; set; } = filename;
    public string Author { get; set; } = author;
    public string Alt { get; set; } = alt;
    public DateTime Uploaded { get; set; } = DateTime.Now;
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
    /// Generates a unique filename for an uploaded image.
    /// Format: First 10 chars of alt + "_meow_" + last 6 Unix timestamp digits + extension.
    /// </summary>
    /// <param name="ext">The file extension (e.g., ".jpg").</param>
    /// <param name="alt">The alt-text used in filename generation.</param>
    /// <returns>The generated filename.</returns>
    public static string GenFilename(string ext, string alt) 
    {
        var currentTime = DateTimeOffset.Now.ToUnixTimeSeconds();
        return $"{alt.Substring(0,10)}_meow_{currentTime.ToString().Substring(currentTime.ToString().Length - 6)}{ext}";
    }

    /// <summary>
    /// Uploads an image to the filesystem and registers it in the database.
    /// </summary>
    /// <param name="request">The upload request parameters.</param>
    /// <param name="database">Initialized database connection.</param>
    /// <returns>
    /// - Bad Request if fields are missing;
    /// - Unauthorized if the author is not an admin;
    /// - Unprocessable Entity for invalid file type or size (>8MB);
    /// - OK.
    /// </returns>
    public static async Task<IResult> Upload(UploadRequest request, Database database)
    {
        if (request.Image == null || string.IsNullOrWhiteSpace(request.Author) || string.IsNullOrWhiteSpace(request.Alt))
            return Results.BadRequest("One or more of the request fields is not specified!");

        if (request.Image.Length > 20_000_000) 
            return Results.UnprocessableEntity("Uploading images bigger then 8 MB in size is not supported.");

        var admin = await Admins.GetAdminByToken(request.Author, database);
        if (admin == null || !admin.Roles.Contains("general")) 
            return Results.Unauthorized();

        var fileext = Path.GetExtension(request.Image.FileName);
        if (fileext != ".jpeg" && fileext != ".jpg" && fileext != ".png") 
            return Results.UnprocessableEntity("We only support .jpeg, .jpg and .png images.");

        var imgFileName = GenFilename(fileext, request.Alt);
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenCafe/fs/img");
        
        using (var stream = new FileStream(Path.Combine(dir, imgFileName), FileMode.Create)) 
        {
            await request.Image.CopyToAsync(stream);
        }

        var imageCollection = database._database.GetCollection<Image>("images");
        await imageCollection.InsertOneAsync(new Image(filename: imgFileName, author: admin.Token, alt: request.Alt));

        return Results.Ok("fs/img/" + imgFileName);
    }
    
    /// <summary>
    /// Deletes an image from the filesystem and database.
    /// </summary>
    /// <param name="request">The delete request parameters.</param>
    /// <param name="database">Initialized database connection.</param>
    /// <returns>
    /// - Bad Request if fields are missing.
    /// - Unauthorized if the token is invalid.
    /// - Not Found if the image doesn't exist.
    /// - OK.
    /// </returns>
    public static async Task<IResult> Delete(DeleteRequest request, Database database) 
    {
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.ID.ToString())) 
            return Results.BadRequest("One or more of the request fields is not specified!");
        
        var isAdmin = await Admins.Login(new Admins.LoginRequest(request.Token), database);
        if (isAdmin is UnauthorizedResult) 
            return isAdmin;

        var imageCollection = database._database.GetCollection<Image>("images");
        var image = await imageCollection.Find(img => img._id == request.ID).FirstOrDefaultAsync();
        if (image == null)
            return Results.NotFound("An image with the specified ID was not found in the database!");
        
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenCafe/fs/img");
        await Task.Run(() => File.Delete(Path.Combine(dir, image.Filename)));

        var oper = await imageCollection.DeleteOneAsync(new BsonDocument("_id", image._id));
        return Results.Ok(oper);
    }
    
    /// <summary>
    /// Retrieves all images from the database.
    /// </summary>
    /// <param name="database">Initialized database connection.</param>
    /// <returns>OK with a list of all images.</returns>
    public static async Task<IResult> GetAll(Database database)
    {
        var imageCollection = database._database.GetCollection<Image>("images");
        var images = await imageCollection.Find(_ => true).ToListAsync();

        return Results.Ok(images);
    }
    /// <summary>
    /// Retrieves a single image by its ID.
    /// </summary>
    /// <param name="_id">The MongoDB ObjectId of the image.</param>
    /// <param name="database">Initialized database connection.</param>
    /// <returns>
    ///  - Not Found if the image doesn't exist;
    /// - OK.
    /// </returns>
    public static async Task<IResult> GetOne(ObjectId _id, Database database)
    {
        var imageCollection = database._database.GetCollection<Image>("images");
        var image = await imageCollection.Find(img => img._id == _id).FirstOrDefaultAsync();
        if (image == null)
            return Results.NotFound();

        return Results.Ok(image);
    }
}

