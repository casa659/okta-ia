using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;

namespace OktaIA.Web.Pages.Admin;

/// <summary>
/// Visão geral da operação — a saúde REAL da plataforma e do monitoramento.
///
/// ⚠️ ESTA TELA ERA INTEIRAMENTE INVENTADA até 07/09/2026: "MRR R$ 284,7k", "8 organizações",
/// "6.788 ativos licenciados", um feed de atividade com pessoas e empresas que não existem
/// ("Escritório Lemos", "Diego Moraes", "Banco Meridiano") e uma saúde de plataforma com
/// "18,4k EPS" e "2,4 bi docs". Tudo escrito à mão em `AdminCatalog`.
///
/// É o mesmo defeito já corrigido na `/Relatorios`, e a razão vale mais aqui: num produto de
/// SEGURANÇA, uma tela de administração aberta na frente de um cliente com número fictício
/// destrói a confiança em todo o resto — inclusive nos números que são verdadeiros.
///
/// ⚠️ ZERO MEDIDO ≠ NUNCA MEDIDO. Onde não há conector, a tela diz que não há monitoramento em
/// vez de mostrar "0 alertas" — "0" lido como resultado afirma que está tudo limpo quando na
/// verdade ninguém olhou. Mesma regra da `/Conformidade`.
/// </summary>
[Authorize]
public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db) { _db = db; }

    // ── Empresas e usuários ──────────────────────────────────────────────────────────────
    public int EmpresasAtivas { get; private set; }
    public int EmpresasComConector { get; private set; }
    public int UsuariosAtivos { get; private set; }

    // ── Conectores ───────────────────────────────────────────────────────────────────────
    public int ConectoresTotal { get; private set; }
    public int ConectoresAtivos { get; private set; }
    public int ConectoresComErro { get; private set; }
    public int ConectoresPausados { get; private set; }
    public int ConectoresNuncaConectados { get; private set; }

    // ── Monitoramento (o que o Wazuh e os demais trouxeram) ─────────────────────────────
    public int Alertas24h { get; private set; }
    public int Alertas7d { get; private set; }
    public int AlertasGravesAbertos { get; private set; }

    /// <summary>Máquinas que APARECERAM em alerta — a prova de que estão reportando.</summary>
    public int MaquinasReportando { get; private set; }

    /// <summary>A sincronização mais recente de qualquer conector. Nula = nunca sincronizou.</summary>
    public ExecucaoSync? UltimoSync { get; private set; }
    public string? UltimoSyncEmpresa { get; private set; }

    /// <summary>
    /// Há monitoramento de verdade? Falso = a tela não mostra número de alerta nenhum.
    ///
    /// ⚠️ Sem conector, "0 alertas nas últimas 24 h" é uma frase verdadeira que comunica uma
    /// mentira: soa como "nada aconteceu", quando o certo é "não estamos olhando".
    /// </summary>
    public bool TemMonitoramento => ConectoresTotal > 0;

    public List<AdminAuditLog> AtividadeRecente { get; private set; } = new();

    public async Task OnGetAsync()
    {
        var agora = DateTimeOffset.UtcNow;

        EmpresasAtivas = await _db.Companies.CountAsync(c => c.Ativo);
        UsuariosAtivos = await _db.Users.CountAsync();

        var conectores = await _db.Conectores.AsNoTracking()
            .Select(c => new { c.CompanyId, c.Status })
            .ToListAsync();

        ConectoresTotal = conectores.Count;
        ConectoresAtivos = conectores.Count(c => c.Status == StatusConector.Ativo);
        ConectoresComErro = conectores.Count(c => c.Status == StatusConector.Erro);
        ConectoresPausados = conectores.Count(c => c.Status == StatusConector.Pausado);
        ConectoresNuncaConectados = conectores.Count(c => c.Status == StatusConector.NuncaConectado);
        EmpresasComConector = conectores.Select(c => c.CompanyId).Distinct().Count();

        if (TemMonitoramento)
        {
            // ⚠️ `IngeridoEm`, e não `OcorridoEm`: aqui se mede o que a plataforma RECEBEU. Um
            // alerta de ontem que chegou hoje é trabalho de hoje — e, se a ingestão parar, é
            // justamente isso que precisa aparecer como queda.
            var desde24 = agora.AddHours(-24);
            var desde7 = agora.AddDays(-7);

            Alertas24h = await _db.AlertasUnificados.CountAsync(a => a.IngeridoEm >= desde24);
            Alertas7d = await _db.AlertasUnificados.CountAsync(a => a.IngeridoEm >= desde7);

            AlertasGravesAbertos = await _db.AlertasUnificados
                .CountAsync(a => a.Severidade >= Severidade.Alta && a.TriadoEm == null);

            MaquinasReportando = await _db.AlertasUnificados
                .Where(a => a.IngeridoEm >= desde7 && a.AtivoNome != null)
                .Select(a => a.AtivoNome)
                .Distinct()
                .CountAsync();

            UltimoSync = await _db.ExecucoesSync.AsNoTracking()
                .OrderByDescending(e => e.IniciadoEm)
                .FirstOrDefaultAsync();

            if (UltimoSync is not null)
            {
                UltimoSyncEmpresa = await _db.Conectores.AsNoTracking()
                    .Where(c => c.Id == UltimoSync.ConectorId)
                    .Select(c => c.Company!.Nome)
                    .FirstOrDefaultAsync();
            }
        }

        // A trilha de auditoria REAL substitui o feed inventado. Vazia é resposta legítima:
        // significa que ninguém mexeu na administração ainda.
        AtividadeRecente = await _db.AdminAuditLogs.AsNoTracking()
            .OrderByDescending(a => a.CriadoEm)
            .Take(8)
            .ToListAsync();
    }

    /// <summary>"há 4 min", "há 3 h", "ontem" — a idade importa mais que o carimbo exato.</summary>
    public static string Quando(DateTimeOffset quando)
    {
        var d = DateTimeOffset.UtcNow - quando;
        if (d.TotalMinutes < 1) { return "agora"; }
        if (d.TotalMinutes < 60) { return $"há {(int)d.TotalMinutes} min"; }
        if (d.TotalHours < 24) { return $"há {(int)d.TotalHours} h"; }
        if (d.TotalDays < 2) { return "ontem"; }
        return $"há {(int)d.TotalDays} dias";
    }

    public static string Quando(DateTime quando) =>
        Quando(new DateTimeOffset(DateTime.SpecifyKind(quando, DateTimeKind.Utc)));
}
