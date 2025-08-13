using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using NoLock.Social.Core.Extensions;
using NoLock.Social.Web;
using TG.Blazor.IndexedDB;
using System.Collections.Generic;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddIndexedDB(dbStore =>
{
    dbStore.DbName = "NoLockSocialDB";
    dbStore.Version = 2;
    
    dbStore.Stores.Add(new StoreSchema
    {
        Name = "content_addressable_storage",
        PrimaryKey = new IndexSpec { Name = "hash", KeyPath = "hash", Unique = true }
    });
});

builder.Services.AddContentAddressableStorage();
builder.Services.AddCryptographicServices();
builder.Services.AddSecurityServices();
builder.Services.AddPerformanceMonitoring();

var app = builder.Build();

// Apply security headers on startup
var securityService = app.Services.GetRequiredService<NoLock.Social.Core.Security.ISecurityService>();
await securityService.ApplySecurityHeadersAsync();
await securityService.ConfigureSecureCookiesAsync();

// Validate CSP
var cspValid = await securityService.ValidateCspAsync();
if (!cspValid)
{
    Console.WriteLine("Warning: Content Security Policy validation failed");
}

// Configure performance monitoring thresholds
var performanceService = app.Services.GetRequiredService<NoLock.Social.Core.Performance.IPerformanceMonitoringService>();

// Set thresholds for critical operations
performanceService.SetThresholds("ContentSigning", new NoLock.Social.Core.Performance.PerformanceThresholds
{
    MaxDuration = TimeSpan.FromMilliseconds(500),
    MaxMemoryBytes = 10 * 1024 * 1024, // 10MB
    MinSuccessRate = 95
});

performanceService.SetThresholds("SignatureVerification", new NoLock.Social.Core.Performance.PerformanceThresholds
{
    MaxDuration = TimeSpan.FromMilliseconds(200),
    MaxMemoryBytes = 5 * 1024 * 1024, // 5MB
    MinSuccessRate = 99
});

await app.RunAsync();
