using Microsoft.Extensions.FileProviders;
using MongoDB.Bson;
using server;
using server.Collections;

var dbs = new DBService();
var db = await dbs.Start();

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

// Customer-related API requests
app.MapPut("/api/customer/register", 
    async (Customers.RegisterRequest request) => await Customers.Register(request, db));

app.MapPost("/api/customer/login", 
    async (Customers.EmailPasswordRequest request) =>await Customers.Login(request, db));


// Loyalty-card related API requests
app.MapPut("/api/card/issue",
    async (Cards.OIIDRequest request) => await Cards.Issue(request, db));

app.MapPost("/api/card/get",
    async (Customers.EmailPasswordRequest request) => await Cards.Get(request, db));

app.MapGet("/api/card/verify",
    async (HttpRequest request) =>
    {
        var id = Int64.TryParse(request.Query["id"], out var num);
        var req = new Cards.IDRequest(num);
        var res = await Cards.Verify(req, db);
        return res; 
    });

// Admin-related API requests.
app.MapGet("/api/admin/login", 
    async (HttpRequest request) => 
    {
        var req = new Admins.LoginRequest(request.Query["token"]);
        var res = await Admins.Login(req, db);
        return res;
    });

app.MapPut("/api/admin/register", 
    async (Admins.RegisterRequest request) => await Admins.Register(request, db));

app.MapPut("/api/admin/changename", 
    async (Admins.ChangeNameRequest request) => await Admins.ChangeName(request, db));

app.MapDelete("/api/admin/delete", 
    async (HttpRequest request) => 
    {
        var req = new Admins.DeleteRequest(request.Query["token1"], request.Query["token2"]);
        return await Admins.Delete(req, db);
    });

app.MapGet("/api/admin/getAll", 
    async (HttpRequest request) =>
    {
        var req = new Admins.GetAllRequest(request.Query["token"]);
        return await Admins.GetAll(req, db);
    });

// Point-related API requests.
app.MapPut("/api/points/new",
     async (Points.AddRequest request) => await Points.New(request, db));

app.MapPatch("/api/points/update",
    async (Points.UpdateRequest request) => await Points.Update(request, db));

app.MapDelete("/api/points/delete", 
    async (HttpRequest request) => 
    {
        var req = new Points.PIDTRequest(Int32.Parse(request.Query["pointID"]), request.Query["token"]);
        return await Points.Delete(req, db);
    });

app.MapPatch("/api/points/adminmgmt",
     async (Points.TwoAdminRequest request) => await Points.PointAdminActions(request, db));

app.MapGet("/api/points/load",
    async (HttpRequest request) =>
    {
        return await Points.LoadByPID(Int32.Parse(request.Query["pointID"]), db);
    });

app.MapGet("/api/points/loadAll",
    async () => { return await Points.LoadAll(db); });  

// Issue-related API requests.
app.MapPut("/api/issues/raise",
    async (Issues.RaiseRequest request) => await Issues.Raise(request, db));

app.MapGet("/api/issues/getAll",
    async (HttpRequest request) =>
    {
        var req = new Admins.GetAllRequest(request.Query["token"]);
        return await Issues.GetAll(req, db);
    });

app.MapPatch("/api/issues/modify",
    async (Issues.ModifyRequest request) => await Issues.Modify(request, db));

// Instance-related API requests.
app.MapPut("/api/instances/flash",
    async (InstanceMgmt.FlashRequest request) => await InstanceMgmt.Flash(request, db));

app.MapGet("/api/instances/restore",
    async (HttpRequest request) =>
    {
        var req = new InstanceMgmt.IdRequest(request.Query["token"], ObjectId.Parse(request.Query["id"]));
        return await InstanceMgmt.Restore(req, db);   
    });

app.MapDelete("/api/instances/delete",
    async (HttpRequest request) =>
    {
        var req = new InstanceMgmt.IdRequest(request.Query["token"], ObjectId.Parse(request.Query["id"]));
        return await InstanceMgmt.Delete(req, db);   
    });

app.MapGet("/api/instances/copy",
    async (HttpRequest request) =>
    {
        var req = new InstanceMgmt.IdRequest(request.Query["token"], ObjectId.Parse(request.Query["id"]));
        return await InstanceMgmt.Copy(req, db);   
    });

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

app.MapDelete("/api/images/delete", 
    async (HttpRequest request) => 
    {
        var req = new Images.DeleteRequest(ObjectId.Parse(request.Query["id"]), request.Query["token"]);
        return await Images.Delete(req, db);
    });

app.MapGet("/api/images/getAll",
     async () => { return await Images.GetAll(db); } );

app.MapGet("/api/images/get", 
    async (HttpRequest request) => { return await Images.GetOne(ObjectId.Parse(request.Query["id"]), db); });

app.Run();



