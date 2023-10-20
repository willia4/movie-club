using System.Security.Claims;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddMicrosoftIdentityWebAppAuthentication(builder.Configuration, "AzureAdB2C");
builder.Services
    .AddRazorPages(options =>
    {
        options.Conventions.AuthorizePage("/index", policy: "Admin");
    })
    .AddMicrosoftIdentityUI();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy =>
    {
        var superUserId = builder.Configuration.GetSection("Superuser").GetValue<string>("ObjectId");

        policy.RequireAssertion(ctx =>
        {
            var user = ctx.User;
            return
                user.HasClaim(c =>
                    !string.IsNullOrWhiteSpace(superUserId) &&
                    c.Type == ClaimTypes.NameIdentifier &&
                    string.Equals(c.Value, superUserId, StringComparison.InvariantCultureIgnoreCase))
                || user.HasClaim(c => c.Type == "Role" && c.Value == "Admin");
        });
    });

    options.AddPolicy("Member", policy =>
    {
        policy.RequireClaim("Role", new string[] { "Member", "Admin" });
    });
});

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

app.MapRazorPages();
app.MapControllers();
app.Run();
