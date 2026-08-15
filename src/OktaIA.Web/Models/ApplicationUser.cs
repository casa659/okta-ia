using Microsoft.AspNetCore.Identity;

namespace OktaIA.Web.Models;

/// <summary>
/// Conta de acesso à plataforma. Existem dois tipos, e a diferença é <see cref="CompanyId"/>:
///
/// - <b>Interno (MSSP)</b> — `CompanyId` nulo. É o analista/operador: vê a visão consolidada de
///   todas as empresas geridas e troca de contexto pelo seletor no cabeçalho.
///
/// - <b>Cliente</b> — `CompanyId` preenchido. Fica <b>preso</b> à própria empresa: o seletor de
///   organização não aparece, e o resolvedor ignora cookie e query string. Sem isso, bastava trocar
///   a organização no cabeçalho (ou editar o cookie) para ver o ambiente de qualquer outro cliente —
///   e o modelo de negócio exige dar login a cliente.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string? NomeCompleto { get; set; }
    public string? Iniciais { get; set; }

    /// <summary>Nulo = usuário interno (vê todas). Preenchido = usuário de cliente, preso a essa empresa.</summary>
    public int? CompanyId { get; set; }
    public Company? Company { get; set; }
}
