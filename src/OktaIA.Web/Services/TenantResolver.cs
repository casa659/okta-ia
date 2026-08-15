using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;

namespace OktaIA.Web.Services;

/// <summary>
/// Resolve a organização (tenant) da requisição — e é o ponto onde o isolamento entre clientes é
/// garantido.
///
/// Antes, a empresa vinha só do cookie "okia_tenant", que é texto puro: qualquer usuário
/// autenticado trocava a organização no cabeçalho (ou editava o cookie) e via o ambiente de
/// QUALQUER cliente. Não era falta de permissão de tela — era ausência de isolamento de dado.
///
/// Agora: usuário com <see cref="FabricaDeClaimsDoUsuario.ClaimEmpresa"/> (conta de cliente) fica
/// PRESO àquela empresa; cookie e query string são ignorados. Usuário sem a claim (interno/MSSP)
/// mantém o comportamento de sempre.
/// </summary>
public static class TenantResolver
{
    public const string CookieName = "okia_tenant";

    /// <summary>Empresa à qual o usuário está preso, ou null se for interno (vê todas).</summary>
    public static int? EmpresaFixaDoUsuario(ClaimsPrincipal? user)
    {
        var valor = user?.FindFirst(FabricaDeClaimsDoUsuario.ClaimEmpresa)?.Value;
        return int.TryParse(valor, out var id) ? id : null;
    }

    /// <summary>true quando o seletor de organização não deve sequer aparecer.</summary>
    public static bool UsuarioPreso(ClaimsPrincipal? user) => EmpresaFixaDoUsuario(user).HasValue;

    /// <summary>
    /// Empresas que este usuário pode enxergar NA LISTA. Não basta isolar o dado: o seletor de
    /// empresa mostrava todas as organizações ativas, então um cliente leria os NOMES da carteira
    /// inteira de clientes — vazamento comercial, mesmo sem acessar o ambiente deles.
    /// </summary>
    public static IQueryable<Company> EmpresasVisiveis(HttpContext context, ApplicationDbContext db)
    {
        var fixa = EmpresaFixaDoUsuario(context.User);
        var query = db.Companies.Where(c => c.Ativo);
        return fixa.HasValue ? query.Where(c => c.Id == fixa.Value) : query;
    }

    public static async Task<Company?> ResolverAtualAsync(HttpContext context, ApplicationDbContext db)
    {
        var fixa = EmpresaFixaDoUsuario(context.User);
        if (fixa.HasValue)
        {
            // Conta de cliente: só a própria empresa, e só se estiver ativa.
            return await db.Companies.FirstOrDefaultAsync(c => c.Id == fixa.Value && c.Ativo);
        }

        var tenants = await db.Companies.Where(c => c.Ativo).OrderBy(c => c.Id).ToListAsync();
        var cookie = context.Request.Cookies[CookieName];
        return tenants.FirstOrDefault(t => t.Id.ToString() == cookie) ?? tenants.FirstOrDefault();
    }

    /// <summary>
    /// Versão para telas com filtro local por empresa (`?empresa=N`). Existe porque corrigir só o
    /// resolvedor acima deixaria ESTE caminho aberto: as telas de Ativos, Vulnerabilidades, Alertas,
    /// Digital Twin e Conectores aceitavam qualquer id vindo da barra de endereço.
    ///
    /// Para conta de cliente o parâmetro é simplesmente descartado.
    /// </summary>
    public static async Task<Company?> ResolverComFiltroAsync(
        HttpContext context, ApplicationDbContext db, int? empresaParam)
    {
        var fixa = EmpresaFixaDoUsuario(context.User);
        if (fixa.HasValue)
        {
            return await db.Companies.FirstOrDefaultAsync(c => c.Id == fixa.Value && c.Ativo);
        }

        if (empresaParam.HasValue)
        {
            var escolhida = await db.Companies.FirstOrDefaultAsync(c => c.Id == empresaParam.Value && c.Ativo);
            if (escolhida is not null)
            {
                return escolhida;
            }
        }

        return await ResolverAtualAsync(context, db);
    }
}
