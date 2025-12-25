WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Load seeder configuration from JSON file
builder.Configuration.AddJsonFile("SeederConfig.json", optional: false, reloadOnChange: true);
builder.Services.Configure<SeederConfiguration>(builder.Configuration.GetSection("SeederConfiguration"));

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddDeliveryApi()
    .AddComposers()
    .Build();

// // Seeders are executed in registration order

// Phase 1: Languages (required for dictionaries and variant content)
builder.Services.AddHostedService<LanguageSeeder>();

// Phase 2: Dictionaries (depends on Languages)
builder.Services.AddHostedService<DictionarySeeder>();

// Phase 3: Data Types (required for Document Types)
builder.Services.AddHostedService<DataTypeSeeder>();

// Phase 4: Document Types with Templates (depends on Data Types)
builder.Services.AddHostedService<DocumentTypeSeeder>();

// Phase 5: Media (independent, but needed for complex content)
builder.Services.AddHostedService<MediaSeeder>();

// Phase 6: Content (depends on Document Types and Media)
builder.Services.AddHostedService<ContentSeeder>();

// Phase 7: Users (independent)
builder.Services.AddHostedService<UserSeeder>();

WebApplication app = builder.Build();

await app.BootUmbracoAsync();


app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseInstallerEndpoints();
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
