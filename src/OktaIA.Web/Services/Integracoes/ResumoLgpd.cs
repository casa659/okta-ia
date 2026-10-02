using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;

namespace OktaIA.Web.Services.Integracoes;

/// <summary>
/// O que esta plataforma entrega ao LinkEscola sobre UMA empresa.
///
/// 🔴 CONTAGENS E TÍTULOS, nunca o alerta inteiro. O que o DPO precisa demonstrar é que o
/// monitoramento existe e que o que apareceu foi tratado — e isso se demonstra com números. O
/// payload bruto de um alerta carrega usuário, IP, nome de máquina: copiá-lo para outro sistema
/// multiplicaria o dado pessoal em vez de proteger, e é o oposto do que a integração serve.
///
/// ⚠️ Mesma decisão que o `/postura/trilha` do LinkEscola tomou na direção contrária. As duas
/// pontas combinam: cada uma conta o que mediu, nenhuma entrega a linha.
/// </summary>
public record ResumoLgpdDto(
    int EmpresaId,
    string Empresa,
    bool Demo,
    DateTimeOffset Em,
    ContagemAlertas Alertas,
    IReadOnlyList<ConectorNoResumo> Conectores,
    IReadOnlyList<AlertaNoResumo> AbertosMaisAntigos,
    string? Aviso);

/// <summary>
/// ⚠️ <paramref name="Abertos"/> é Novo + EmAndamento, e NÃO "total menos resolvidos": falso
/// positivo não está aberto nem foi trabalho — somá-lo a qualquer um dos dois lados mentiria sobre
/// o tamanho da fila ou sobre o que a equipe fez.
/// </summary>
public record ContagemAlertas(
    int Total,
    int Novos,
    int EmAndamento,
    int Resolvidos,
    int FalsosPositivos,
    int Abertos,
    int CriticosAbertos,
    int AltosAbertos,
    int UltimosTrintaDias,
    double? PercentualTratado);

public record ConectorNoResumo(
    string Nome,
    string Fabricante,
    string Status,
    DateTimeOffset? UltimoSyncEm,
    string? UltimoErro);

/// <summary>
/// Um alerta aberto, resumido ao que o DPO precisa para cobrar andamento.
///
/// ⚠️ SEM ativo, IP ou usuário. O título já diz o que é; o resto é dado de infraestrutura — e dado
/// pessoal, no caso do usuário — que não ajuda a cobrar e aumenta a exposição.
/// </summary>
public record AlertaNoResumo(
    string Titulo,
    string Severidade,
    string Status,
    DateTimeOffset OcorridoEm,
    int DiasAberto,
    string? Responsavel);

public class ResumoLgpd
{
    private readonly ApplicationDbContext _db;

    public ResumoLgpd(ApplicationDbContext db) => _db = db;

    public async Task<ResumoLgpdDto> MedirAsync(Company empresa, CancellationToken ct = default)
    {
        var agora = DateTimeOffset.UtcNow;
        var trintaDias = agora.AddDays(-30);

        var daEmpresa = _db.AlertasUnificados.Where(a => a.CompanyId == empresa.Id);

        // Uma passada só, agrupando por status. Quatro consultas separadas dariam o mesmo número e
        // quatro idas ao banco — e, pior, poderiam ler estados diferentes se um sync gravasse no
        // meio, produzindo um total que não fecha com as partes.
        var porStatus = await daEmpresa
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Qtd = g.Count() })
            .ToListAsync(ct);

        int Qtd(StatusTriagem s) => porStatus.FirstOrDefault(x => x.Status == s)?.Qtd ?? 0;

        var novos = Qtd(StatusTriagem.Novo);
        var andamento = Qtd(StatusTriagem.EmAndamento);
        var resolvidos = Qtd(StatusTriagem.Resolvido);
        var falsos = Qtd(StatusTriagem.FalsoPositivo);
        var total = novos + andamento + resolvidos + falsos;
        var abertos = novos + andamento;

        var emAberto = daEmpresa.Where(a =>
            a.Status == StatusTriagem.Novo || a.Status == StatusTriagem.EmAndamento);

        var criticos = await emAberto.CountAsync(a => a.Severidade == Severidade.Critica, ct);
        var altos = await emAberto.CountAsync(a => a.Severidade == Severidade.Alta, ct);
        var recentes = await daEmpresa.CountAsync(a => a.OcorridoEm >= trintaDias, ct);

        // ⚠️ Denominador = o que EXIGIA decisão (tudo menos os que ainda nem foram olhados não
        // serve: o que não foi olhado é justamente o que falta). Então: tratados sobre o total,
        // contando falso positivo como tratado — olhar e concluir "não era nada" é trabalho feito.
        double? percentual = total == 0 ? null
            : Math.Round((resolvidos + falsos) * 100.0 / total, 1);

        // ⚠️ Ordena pelo que ACONTECEU, não pelo que ingerimos: um alerta antigo lido hoje continua
        // sendo um alerta antigo, e é a idade real que diz se alguém está deixando a fila envelhecer.
        var maisAntigos = await emAberto
            .OrderBy(a => a.OcorridoEm)
            .Take(10)
            .Select(a => new { a.Titulo, a.Severidade, a.Status, a.OcorridoEm, a.Responsavel })
            .ToListAsync(ct);

        var conectores = await _db.Conectores
            .Where(c => c.CompanyId == empresa.Id)
            .OrderBy(c => c.Nome)
            .Select(c => new ConectorNoResumo(
                c.Nome, c.Fabricante, c.Status.ToString(), c.UltimoSyncEm, c.UltimoErro))
            .ToListAsync(ct);

        // 🔴 O AVISO VIAJA JUNTO. Empresa de demonstração tem alerta inventado, e um número
        // inventado chegando a uma tela de conformidade seria lido como real pelo DPO — que
        // responde por ele. Quem recebe precisa poder dizer isso na tela, então a marca vem no
        // próprio resumo e não só no cadastro de lá.
        var aviso = empresa.Demo
            ? "Empresa de demonstração: os alertas são fictícios e não representam o ambiente real."
            : conectores.Count == 0
                ? "Nenhum conector instalado nesta empresa: não há leitura de alertas acontecendo."
                : null;

        return new ResumoLgpdDto(
            empresa.Id,
            empresa.Nome,
            empresa.Demo,
            agora,
            new ContagemAlertas(total, novos, andamento, resolvidos, falsos, abertos,
                criticos, altos, recentes, percentual),
            conectores,
            maisAntigos.Select(a => new AlertaNoResumo(
                a.Titulo,
                a.Severidade.ToString(),
                a.Status.ToString(),
                a.OcorridoEm,
                (int)Math.Floor((agora - a.OcorridoEm).TotalDays),
                a.Responsavel)).ToList(),
            aviso);
    }
}
