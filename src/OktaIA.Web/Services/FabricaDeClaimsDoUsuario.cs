using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using OktaIA.Web.Models;

namespace OktaIA.Web.Services;

/// <summary>
/// Carimba a empresa do usuário no cookie de autenticação, como claim.
///
/// Por que claim e não consulta ao banco: o resolvedor de empresa roda em toda página (inclusive no
/// layout) e precisa ser barato. A claim viaja no cookie de autenticação, que é assinado e cifrado
/// pelo ASP.NET — o usuário não consegue forjá-la, diferente do cookie de organização, que é texto
/// puro e foi justamente o furo que estamos fechando.
///
/// Aplicada por esta fábrica em vez de no código de login: assim vale para TODO caminho de
/// autenticação (senha, 2FA, "lembrar deste dispositivo") sem precisar lembrar de cada um.
/// </summary>
public class FabricaDeClaimsDoUsuario : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public const string ClaimEmpresa = "okia_empresa";

    public FabricaDeClaimsDoUsuario(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        // Só usuário de cliente recebe a claim. Interno fica sem — e "sem claim" significa
        // "vê todas as empresas", que é o comportamento que já existia.
        if (user.CompanyId.HasValue)
        {
            identity.AddClaim(new Claim(ClaimEmpresa, user.CompanyId.Value.ToString()));
        }

        return identity;
    }
}
