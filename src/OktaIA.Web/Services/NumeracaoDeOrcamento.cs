using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;

namespace OktaIA.Web.Services;

/// <summary>
/// O próximo número de orçamento (ORC-2026-000001…).
///
/// ⚠️ UMA CÓPIA SÓ (06/09/2026). Vivia como método privado de `OrcamentosModel`; quando a
/// importação de planilha passou a criar orçamento sozinha, ela precisou do mesmo número — e
/// copiar teria dado às duas portas a chance de gerar o mesmo número em paralelo com regras
/// levemente diferentes um dia.
/// </summary>
public static class NumeracaoDeOrcamento
{
    public static async Task<string> ProximoAsync(ApplicationDbContext db)
    {
        var prefixo = $"ORC-{DateTime.UtcNow.Year}-";
        var ultimo = await db.Orcamentos
            .Where(p => p.Numero.StartsWith(prefixo))
            .OrderByDescending(p => p.Numero)
            .Select(p => p.Numero)
            .FirstOrDefaultAsync();

        var n = 1;
        if (ultimo is not null && int.TryParse(ultimo[prefixo.Length..], out var atual)) { n = atual + 1; }
        return prefixo + n.ToString("D6");
    }
}
