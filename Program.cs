using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using zinfandel_movie_club.Data;
using zinfandel_movie_club.Authentication;
using zinfandel_movie_club.Config;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddMicrosoftIdentityWebAppAuthentication(builder.Configuration, "AzureAdB2C");
builder.Services
    .AddRazorPages(options =>
    {
        options.Conventions.AuthorizePage("/Privacy", policy: "Admin");
        options.Conventions.AuthorizeFolder("/Admin", policy: "Admin");
    })
    .AddMicrosoftIdentityUI();

builder.Services.AddSingleton<IAuthorizationHandler, AdminAuthorizationPolicy>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy =>
    {
        policy.AddRequirements(new IAuthorizationRequirement[] { new AdminAuthorizationRequirement() });
    });
});

builder.Services.Configure<GraphApi>(builder.Configuration.GetSection("GraphApi"));
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));
builder.Services.Configure<DatabaseConfig>(builder.Configuration.GetSection("Database"));
builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<DatabaseConfig>>().Value.Cosmos);

builder.Services.AddSingleton<IUserProfileDatabase, UserProfileDatabase>();
builder.Services.AddSingleton<IGraphUserManager, GraphUserManager>();

builder.Services.AddSingleton<IUserManager, UserManager>();
builder.Services.AddMemoryCache();
builder.Services.AddTransient<ClaimRoleDecoratorMiddleware>();



var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();
app.UseMiddleware<ClaimRoleDecoratorMiddleware>();

app.MapRazorPages();
app.MapControllers(); // remove when we replace the Identity.Web.UI default account controller
app.Run();