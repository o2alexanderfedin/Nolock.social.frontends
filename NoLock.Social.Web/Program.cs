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

await builder.Build().RunAsync();
