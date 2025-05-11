using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Globalization;
using panel.Services;
using panel;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");

// Add localization with the correct resources path
builder.Services.AddLocalization(options => options.ResourcesPath = "Strings");

// Register the HttpClient (needed for loading satellite assemblies)
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<Localization>().AddScoped<Credentials>().AddScoped<AdminDetails>();

var host = builder.Build();
var localization = host.Services.GetRequiredService<Localization>();
await localization.InitializeAsync();

await host.RunAsync();

