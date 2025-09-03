using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.Extensions;
using NoLock.Social.Core.Storage;
using Nolock.Social.Storage.IndexedDb;
using NoLock.Social.Web;
using TG.Blazor.IndexedDB;

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
// Register the IndexedDb implementation for ContentData<byte[]>
builder.Services.AddScoped<IContentAddressableStorage<ContentData<byte[]>>, 
    IndexedDbContentAddressableStorage<ContentData<byte[]>>>();
// Use reactive version of cryptographic services
builder.Services.AddCryptographicServices(useReactive: true);
builder.Services.AddSecurityServices();
builder.Services.AddPerformanceMonitoring();
// Add login services (wraps existing identity unlock with login semantics)
builder.Services.AddLoginServices();
// Add camera and accessibility services
builder.Services.AddCameraServices();
builder.Services.AddAccessibilityServices();
// Add OCR services for document processing
builder.Services.AddMinimalOCRServices();

var app = builder.Build();

// Check Web Crypto API availability
var webCrypto = app.Services.GetRequiredService<NoLock.Social.Core.Cryptography.Interfaces.IWebCryptoService>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var webCryptoAvailable = await webCrypto.IsAvailableAsync();
if (!webCryptoAvailable)
{
    logger.LogWarning("Web Crypto API is not available. This application requires a modern browser with HTTPS.");
}
else
{
    logger.LogInformation("Web Crypto API is available and ready");
}

await app.RunAsync();
