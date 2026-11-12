using System.Globalization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.EntityFrameworkCore;
using Zeiterfassung.Web;
using Zeiterfassung.Web.Data;
using Zeiterfassung.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Die Oberfläche ist deutsch; Datums- und Zahlenformate folgen dem.
CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("de-DE");
CultureInfo.DefaultThreadCurrentUICulture = new CultureInfo("de-DE");

// Alles läuft im Browser: InMemory-Datenbank statt MySQL, kein HttpClient, keine Server-Aufrufe.
builder.Services.AddDbContextFactory<ZeiterfassungDbContext>(o => o.UseInMemoryDatabase("zeiterfassung"));
builder.Services.AddScoped<TimeTrackingService>();
builder.Services.AddScoped<TimeTrackingExportService>();
builder.Services.AddScoped<ToastService>();

var host = builder.Build();

// Beispieldaten relativ zu "heute", damit die Demo nie veraltet.
var factory = host.Services.GetRequiredService<IDbContextFactory<ZeiterfassungDbContext>>();
await DemoSeed.SeedAsync(factory, DateTime.Now);

await host.RunAsync();
