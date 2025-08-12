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
    dbStore.Version = 1;
    
    dbStore.Stores.Add(new StoreSchema
    {
        Name = "content_addressable_storage",
        PrimaryKey = new IndexSpec { Name = "hash", KeyPath = "hash", Unique = true },
        Indexes = new List<IndexSpec>
        {
            new IndexSpec { Name = "createdAt", KeyPath = "metadata.createdAt" },
            new IndexSpec { Name = "size", KeyPath = "metadata.size" },
            new IndexSpec { Name = "contentType", KeyPath = "metadata.contentType" }
        }
    });
});

builder.Services.AddContentAddressableStorage();

await builder.Build().RunAsync();
