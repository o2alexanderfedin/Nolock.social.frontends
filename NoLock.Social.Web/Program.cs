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

// Check Web Crypto API availability
var webCrypto = app.Services.GetRequiredService<NoLock.Social.Core.Cryptography.Interfaces.IWebCryptoService>();
var webCryptoAvailable = await webCrypto.IsAvailableAsync();
if (!webCryptoAvailable)
{
    Console.WriteLine("Warning: Web Crypto API is not available. This application requires a modern browser with HTTPS.");
}
else
{
    Console.WriteLine("Web Crypto API is available and ready");
}

await app.RunAsync();
