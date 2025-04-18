using Microsoft.Extensions.FileProviders;
using server;
using server.Collections;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var fspath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenCafe/fs");
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(fspath),
    RequestPath = "/fs"
});

app.MapGet("/", () => "Wilkommen auf OpenCafe!");

var dbs = new DBService();
var db = await dbs.Start();

// Admin-related API requests.

app.MapPost("/api/admin/login", 
    async (Admins.LoginRequest req) => await Admins.Login(req: req, database: db));

app.MapPut("/api/admin/register", 
    async (Admins.RegisterRequest req) => await Admins.Register(req: req, database: db));

app.MapPut("/api/admin/changename", 
    async (Admins.ChangeNameRequest req) => await Admins.ChangeName(req: req, database: db));

app.MapPost("/api/admin/delete",
    async (Admins.DeleteRequest req) => await Admins.Delete(req: req, database: db));

app.MapPost("/api/admin/getAll", 
    async (Admins.GetAllRequest req) => await Admins.GetAll(req: req, database: db));

// Image-related API requests.

app.MapPut("/api/images/upload",
async (HttpContext ctx) => 
    {
        if (ctx.Request.HasFormContentType)
        {
            var form = await ctx.Request.ReadFormAsync();
            var req = new Images.UploadRequest(form.Files["image"], form["author"], form["alt"]);
            return await Images.Upload(req, db);
        }
        return Results.BadRequest();
    });

app.Run();



