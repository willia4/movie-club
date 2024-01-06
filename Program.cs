using System.Collections.Immutable;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using zinfandel_movie_club;
using zinfandel_movie_club.Data;
using zinfandel_movie_club.Authentication;
using zinfandel_movie_club.Config;
using zinfandel_movie_club.Data.Models;
using ApplicationIdentity = zinfandel_movie_club.Config.ApplicationIdentity;

var builder = WebApplication.CreateBuilder(args);

var dataProtectionKeyUrl = builder
    .Configuration
    .GetSection("DataProtection")
    .GetValue<string>("KeyVaultKeyUri");

builder.Services
    .AddDataProtection()
    .PersistKeysToAzureBlobStorage(sp =>
    {
        var connectionString = sp.GetRequiredService<IOptions<DatabaseConfig>>().Value.StorageAccount.ConnectionString;
        var dataProtection = sp.GetRequiredService<IOptions<DataProtectionConfig>>().Value;
        return new Azure.Storage.Blobs.BlobClient(
            connectionString: connectionString,
            blobContainerName: dataProtection.StorageAccountContainer,
            blobName: dataProtection.StorageAccountBlob);
    })
    .ProtectKeysWithAzureKeyVault(dataProtectionKeyUrl, sp => sp.GetRequiredService<Azure.Core.TokenCredential>());

builder.Services.AddMicrosoftIdentityWebAppAuthentication(builder.Configuration, "AzureAdB2C");
builder.Services
    .AddRazorPages(options =>
    {
        options.Conventions.AuthorizePage("/SignIn", policy: "Member");
        options.Conventions.AuthorizePage("/Privacy", policy: "Member");
        options.Conventions.AuthorizePage("/Picker", policy: "Member");
        
        options.Conventions.AuthorizeFolder("/Admin", policy: "Admin");
        options.Conventions.AuthorizeFolder("/Profile", policy: "Member");
        options.Conventions.AuthorizeFolder("/Movies", policy: "Member");
    })
    .AddMicrosoftIdentityUI();

builder.Services.AddSingleton<IImageUrlProvider<MovieDocument>, CoverImageProvider>();
builder.Services.AddSingleton<IImageUrlProvider<IGraphUser>, ProfileImageProvider>();

builder.Services.AddSingleton<ISuperUserIdentifier, SuperUserIdentifier>();
builder.Services.AddSingleton<IAuthorizationHandler, AdminAuthorizationPolicy>();
builder.Services.AddSingleton<IAuthorizationHandler, MemberAuthorizationPolicy>();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy =>
    {
        policy.AddRequirements(new IAuthorizationRequirement[] { new AdminAuthorizationRequirement() });
    });
    options.AddPolicy("Member", policy =>
    {
        policy.AddRequirements(new IAuthorizationRequirement[] { new MemberAuthorizationRequirement() });
    });
});

builder.Services.AddRateLimiter(limiterOptions =>
{
    limiterOptions.OnRejected = (ctx, ct) =>
    {
        ctx.HttpContext.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
        return ValueTask.CompletedTask;
    };

    limiterOptions.AddTokenBucketLimiter(policyName: "api", options =>
    {
        options.TokenLimit = 50;
        options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        options.QueueLimit = 5;
        options.ReplenishmentPeriod = TimeSpan.FromSeconds(20);
        options.TokensPerPeriod = 15;
        options.AutoReplenishment = true;
    });
});

builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
    options.AppendTrailingSlash = false;
});

builder.Services.Configure<GraphApi>(builder.Configuration.GetSection("GraphApi"));
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));
builder.Services.Configure<DatabaseConfig>(builder.Configuration.GetSection("Database"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<DatabaseConfig>>().Value.Cosmos);
builder.Services.Configure<TMDBConfig>(builder.Configuration.GetSection("TheMovieDatabase"));
builder.Services.Configure<ApplicationIdentity>(builder.Configuration.GetSection("ApplicationIdentity"));
builder.Services.Configure<DataProtectionConfig>(builder.Configuration.GetSection("DataProtection"));
builder.Services.AddSingleton<Azure.Core.TokenCredential>(sp =>
{
    var config = sp.GetRequiredService<IOptions<ApplicationIdentity>>().Value;
    return new Azure.Identity.ClientSecretCredential(
        tenantId: config.TenantId,
        clientId: config.ClientId,
        clientSecret: config.ClientSecret);
});

builder.Services.AddScoped<IIdGenerator, IdGenerator>();
builder.Services.AddScoped<MovieIdGenerator>();
builder.Services.AddScoped<UserIdGenerator>();
builder.Services.AddScoped<UserRatingIdGenerator>();

builder.Services.AddSingleton<IUserRoleDecorator, UserRoleDecorator>();
builder.Services.AddSingleton<IUserProfileKeyValueStore, UserProfileKeyValueStore>();
builder.Services.AddSingleton<IGraphUserManager, GraphUserManager>();
builder.Services.AddCosmosDocumentManager<MovieDocument>();
builder.Services.AddCosmosDocumentManager<UserRatingDocument>();

builder.Services.AddSingleton<IImageManager, ImageManager>();
builder.Services.AddSingleton<IShuffleHasher, ShuffleHasher>();
builder.Services.AddSingleton<Branding>();

builder.Services.AddMemoryCache();
builder.Services.AddTransient<ClaimRoleDecoratorMiddleware>();
builder.Services.AddScoped<IMovieDatabase, TheMovieDatabase>();
builder.Services.AddScoped<IUriDownloader, UriDownloader>();
builder.Services.AddScoped<IMovieRatingsManager, MovieRatingsManager>();
builder.Services.AddHttpClient();
builder.Services.AddHostedService<BackgroundTasks>();

var app = builder.Build();
app.UseExceptionHandler("/error");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;

    app.MapGet("/debug/routes", (IEnumerable<EndpointDataSource> endpointSources) =>
        string.Join("\n", endpointSources
            .SelectMany(src => src.Endpoints)
            .Select(ep =>
            {
                var routeString = $"Unknown Route for {ep.GetType().Name}";
                
                if (ep is RouteEndpoint { RoutePattern: var rp } rep)
                {
                    var parameters = rp.Parameters.Select(p => p.Name).ToImmutableList();
                    var parametersString = $"({string.Join(", ", parameters)})";
                    routeString = $"{rp.RawText} - {parametersString}";
                }

                return $"{ep}: {routeString}";
            })));
}

app.MapGet("microsoftidentity/account/signedout", (httpContext) =>
{
    httpContext.Response.Redirect("/");
    return Task.CompletedTask;
});

app.UseForwardedHeaders(new ForwardedHeadersOptions()
{
    ForwardedHeaders = ForwardedHeaders.XForwardedProto
});

app.UseCookiePolicy(new CookiePolicyOptions()
{
    Secure = CookieSecurePolicy.Always
});

app.UseStaticFiles();

app.UseRouting();

var csp = new Lazy<string>(() =>
{
    var config = app.Services.GetRequiredService<IConfiguration>();
    var section = config.GetSection("AzureAdB2C");
    var instance = new Uri(section.GetValue<string>("Instance")!);
    var domain = section.GetValue<string>("domain");
    
    // see https://helpx.adobe.com/fonts/using/content-security-policy.html for specifics around typekit
    var csp = "default-src 'self'; " +
                       "script-src 'self' p.typekit.net use.typekit.net; " +
                       "style-src 'self' 'unsafe-inline' p.typekit.net use.typekit.net cdn.jsdelivr.net data:; " +
                       "img-src 'self' p.typekit.net use.typekit.net cdn.jsdelivr.net image.tmdb.org partypartymovieclub.blob.core.windows.net data:; " +
                       "font-src 'self' p.typekit.net use.typekit.net cdn.jsdelivr.net data:; " +
                       $"connect-src 'self' performance.typekit.net {instance.Host} {domain}";
    return csp;
});

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.Add("Content-Security-Policy", csp.Value);
    await next(ctx);
});
app.UseMiddleware<ClaimRoleDecoratorMiddleware>();
app.UseRateLimiter();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();
app.Run();
