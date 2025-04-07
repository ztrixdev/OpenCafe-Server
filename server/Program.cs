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

app.MapPost("/api/admin/login", 
    async (Admins.LoginRequest req) => await Admins.Login(token: req.Token, database: db));

app.MapPost("/api/admin/register", 
    async (Admins.RegisterRequest req) => await Admins.Register(token: req.Token, database: db, name: req.Name));

app.Run();



