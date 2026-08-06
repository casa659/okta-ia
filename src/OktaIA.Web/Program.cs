using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Data.Seed;
using OktaIA.Web.Models;
using OktaIA.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    // Roda em toda página Razor do site — a própria filter decide se a página está no
    // AreaCatalog; fora dele, passa direto (ver Services/AreaPermissionFilter.cs).
    options.Conventions.ConfigureFilter(new Microsoft.AspNetCore.Mvc.TypeFilterAttribute(typeof(AreaPermissionFilter)));
});
builder.Services.AddHttpContextAccessor();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<I18nService>();
builder.Services.AddScoped<AdminAuditService>();
builder.Services.AddSingleton<RelatorioPdfService>();
builder.Services.AddSingleton<PropostaComercialPdfService>();
builder.Services.AddSingleton<TermoAutorizacaoPdfService>();
builder.Services.AddHttpClient<SecurityScanService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("okta-ia-scanner/1.0");
});

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

var cookieSecurePolicy = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Login";
    options.LogoutPath = "/Login";
    options.AccessDeniedPath = "/AcessoNegado";
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SecurePolicy = cookieSecurePolicy;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.HeaderName = "X-CSRF-TOKEN";
});

var app = builder.Build();

// Mesma correção de cultura usada no Lekker: sem isso o binder de formulário usa a cultura do
// SO (pt-BR), que trata "." como separador de milhar — inputs number sempre mandam "." decimal.
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture(CultureInfo.InvariantCulture.Name)
    .AddSupportedCultures(CultureInfo.InvariantCulture.Name)
    .AddSupportedUICultures(CultureInfo.InvariantCulture.Name));

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    headers["X-Frame-Options"] = "SAMEORIGIN";
    headers["Permissions-Policy"] = "microphone=(), camera=(), geolocation=(), payment=()";
    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        "script-src 'self'; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data:; " +
        "connect-src 'self'; " +
        "frame-ancestors 'self'; " +
        "base-uri 'self'; " +
        "form-action 'self'";
    headers.Remove("X-Powered-By");
    await next();
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.RunAsync(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<IConfiguration>());
}

app.Run();
