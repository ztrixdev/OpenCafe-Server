using Microsoft.AspNetCore.Identity.Data;
using server;
using server.Collections;

var builder = WebApplication.CreateBuilder(args);

var dbs = new DBService();
var db = await dbs.Start();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => "Wilkommen auf OpenCafe!");

app.MapPost("/api/admin/login", 
    async (Admins.LoginRequest req) => await Admins.Login(req: req, database: db));

app.MapPost("/api/admin/register", 
    async (Admins.RegisterRequest req) => await Admins.Register(req: req, database: db));

app.MapPut("/api/admin/changename", 
    async (Admins.ChangeNameRequest req) => await Admins.ChangeName(req: req, database: db));

app.MapPost("/api/admin/delete",
    async (Admins.DeleteRequest req) => await Admins.Delete(req: req, database: db));

app.MapPost("/api/admin/getAll", 
    async (Admins.GetAllRequest req) => await Admins.GetAll(req: req, database: db));

app.Run();



