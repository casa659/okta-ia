using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Data.Seed;
using OktaIA.Web.Models;
using OktaIA.Web.Services;
using OktaIA.Web.Services.Integracoes;

var builder = WebApplication.CreateBuilder(args);

// ---------- Azure Key Vault ----------
// Segredo de verdade (hoje a chave que cifra as credenciais dos conectores) sai do App Setting e
// passa a vir do cofre: deixa de existir como texto puro em configuração, ganha rotação e log de
// acesso. A autenticação é por identidade gerenciada do App Service — nenhuma credencial de acesso
// ao cofre fica no código ou na configuração.
//
// Registrado DEPOIS das fontes padrão, então o cofre tem precedência sobre o App Setting; enquanto
// os dois existirem, o valor é o mesmo e a troca é indolor.
//
// Nome no cofre usa "--" onde a configuração usa ":" (Key Vault não aceita ":"):
//   Integracoes--ChaveCriptografia  ->  Integracoes:ChaveCriptografia
//
// Falha ao alcançar o cofre NÃO derruba o site: sem isto, um problema de rede ou de permissão no
// Azure tiraria o console inteiro do ar por causa de um segredo que só a tela de conectores usa.
// A tela de conectores já avisa sozinha quando a chave não está disponível.
var cofreUri = builder.Configuration["Integracoes:KeyVaultUri"];
if (!string.IsNullOrWhiteSpace(cofreUri))
{
    try
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri(cofreUri),
            new Azure.Identity.DefaultAzureCredential());
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[KeyVault] Não foi possível carregar o cofre {cofreUri}: {ex.Message}");
    }
}

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
builder.Services.AddScoped<CopilotService>();
builder.Services.AddScoped<ScanExecutor>();
builder.Services.AddHostedService<ScanAgendadorService>();
builder.Services.AddScoped<AdminAuditService>();
builder.Services.AddSingleton<RelatorioPdfService>();
builder.Services.AddSingleton<PropostaComercialPdfService>();
builder.Services.AddSingleton<TermoAutorizacaoPdfService>();
builder.Services.AddSingleton<RoteiroPdfService>();
// Sem estado e sem dependência de request — a chave vem de configuração e não muda em execução.
builder.Services.AddSingleton<ProtetorDeCredencial>();
builder.Services.AddScoped<RegistroDeConectores>();
builder.Services.AddScoped<MotorDeSync>();
builder.Services.AddHostedService<SyncAgendadorService>();

builder.Services.AddHttpClient<WazuhConnector>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // Instalação padrão do Wazuh Indexer usa certificado autoassinado, e ele fica na rede interna
    // do cliente. Aceitar isso é OPT-IN explícito por configuração: numa plataforma de segurança,
    // desligar validação de TLS em silêncio seria exatamente o tipo de coisa que auditamos nos
    // outros. Fora do laboratório, o certo é o cliente instalar um certificado confiável.
    ServerCertificateCustomValidationCallback =
        builder.Configuration.GetValue("Integracoes:Wazuh:IgnorarCertificado", false)
            ? HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            : null,
});
// Registrado também pela interface pra que o RegistroDeConectores enxergue todos os adaptadores.
builder.Services.AddTransient<IConnector>(sp => sp.GetRequiredService<WazuhConnector>());
builder.Services.AddHttpClient<SecurityScanService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("loktaia-scanner/1.0");
});

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.SignIn.RequireConfirmedAccount = false;
    })
    // Carimba a empresa do usuário como claim no cookie de autenticação — é o que prende conta de
    // cliente à própria organização em TODO caminho de login (senha, 2FA, lembrar dispositivo).
    .AddClaimsPrincipalFactory<FabricaDeClaimsDoUsuario>()
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
