using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;

namespace OktaIA.Web.Services;

// Executa um scan e persiste o resultado — caminho ÚNICO usado tanto pelo botão "Escanear agora"
// (/Ativos) quanto pelo agendador automático. A lógica vivia dentro de Ativos.OnPostScanAsync; foi
// extraída pra cá quando o agendador nasceu, porque duas cópias da mesma persistência divergem com
// o tempo e o scan automático passaria a gravar diferente do manual sem ninguém perceber.
//
// Além de substituir os achados, compara com a varredura anterior e registra as mudanças
// (ScanAlerta) — é isso que permite responder "o que mudou desde ontem" em vez de só "como está".
public class ScanExecutor
{
    private readonly ApplicationDbContext _db;
    private readonly SecurityScanService _scanner;
    private readonly ILogger<ScanExecutor> _log;

    public ScanExecutor(ApplicationDbContext db, SecurityScanService scanner, ILogger<ScanExecutor> log)
    {
        _db = db;
        _scanner = scanner;
        _log = log;
    }

    public record Resultado(int Achados, int Novos, int Resolvidos, bool Executado);

    public static readonly Resultado NaoExecutado = new(0, 0, 0, false);

    public async Task<Resultado> ExecutarAsync(Asset asset, bool automatico, CancellationToken ct = default)
    {
        // Defesa em profundidade repetida aqui (e não só na página): o agendador varre o banco
        // sozinho, então esta é a última barreira antes de disparar tráfego contra um domínio.
        // Ativo do seed tem nome que pode coincidir com domínio real de terceiro — nunca escanear.
        if (!asset.Real || !asset.AutorizadoParaScan)
        {
            return NaoExecutado;
        }

        var resultado = await _scanner.ExecutarAsync(asset.Nome);

        var anteriores = await _db.Vulnerabilities
            .Where(v => v.CompanyId == asset.CompanyId && v.AssetNome == asset.Nome && v.FonteScan)
            .ToListAsync(ct);

        // Identidade de um achado = título + categoria (mesmo critério que o botão "Reverificar"
        // usa). O texto do título já embute o detalhe que importa (ex.: qual porta está aberta).
        static string Chave(string titulo, string? categoria) => $"{categoria}|{titulo}";

        var antesChaves = anteriores.Select(a => Chave(a.TituloPt, a.CategoriaScan)).ToHashSet();
        var agoraChaves = resultado.Achados.Select(a => Chave(a.TituloPt, a.Categoria)).ToHashSet();

        var novos = resultado.Achados.Where(a => !antesChaves.Contains(Chave(a.TituloPt, a.Categoria))).ToList();
        var resolvidos = anteriores.Where(a => !agoraChaves.Contains(Chave(a.TituloPt, a.CategoriaScan))).ToList();

        // Primeira varredura do ativo (nunca escaneado) não gera alerta de "novo achado": tudo
        // seria novo por definição e isso viraria um disparo de N alertas sem significar mudança
        // nenhuma. O histórico de mudanças começa a valer a partir da segunda varredura.
        var primeiraVarredura = asset.UltimoScanEm is null;
        var agora = DateTimeOffset.UtcNow;

        if (!primeiraVarredura)
        {
            foreach (var achado in novos)
            {
                _db.ScanAlertas.Add(new ScanAlerta
                {
                    CompanyId = asset.CompanyId,
                    AssetNome = asset.Nome,
                    Tipo = TipoMudancaScan.Novo,
                    TituloPt = achado.TituloPt,
                    TituloEn = achado.TituloEn,
                    Severidade = achado.Severidade,
                    CategoriaScan = achado.Categoria,
                    DetectadoEm = agora,
                    Automatico = automatico,
                });
            }

            foreach (var achado in resolvidos)
            {
                _db.ScanAlertas.Add(new ScanAlerta
                {
                    CompanyId = asset.CompanyId,
                    AssetNome = asset.Nome,
                    Tipo = TipoMudancaScan.Resolvido,
                    TituloPt = achado.TituloPt,
                    TituloEn = achado.TituloEn,
                    Severidade = achado.Severidade,
                    CategoriaScan = achado.CategoriaScan,
                    DetectadoEm = agora,
                    Automatico = automatico,
                });
            }
        }

        _db.Vulnerabilities.RemoveRange(anteriores);

        foreach (var achado in resultado.Achados)
        {
            _db.Vulnerabilities.Add(new Vulnerability
            {
                CompanyId = asset.CompanyId,
                FonteScan = true,
                CategoriaScan = achado.Categoria,
                RiscoPt = achado.RiscoPt,
                RiscoEn = achado.RiscoEn,
                RecomendacaoPt = achado.RecomendacaoPt,
                RecomendacaoEn = achado.RecomendacaoEn,
                InstrucoesPt = achado.InstrucoesPt,
                InstrucoesEn = achado.InstrucoesEn,
                Cve = "—",
                Cvss = achado.Severidade switch { Severidade.Critica => 9.5m, Severidade.Alta => 7.5m, Severidade.Media => 5.0m, _ => 2.5m },
                Componente = "Perímetro externo",
                TituloPt = achado.TituloPt,
                TituloEn = achado.TituloEn,
                Cwe = "—",
                AssetNome = asset.Nome,
                ExposicaoPt = "Público",
                ExposicaoEn = "Public",
                PrioridadeIa = achado.Severidade switch { Severidade.Critica => 95, Severidade.Alta => 75, Severidade.Media => 45, _ => 20 },
                StatusPt = "Aberto",
                StatusEn = "Open",
                Severidade = achado.Severidade,
            });
        }

        if (!string.IsNullOrWhiteSpace(resultado.Ip))
        {
            asset.Ip = resultado.Ip;
        }

        asset.UltimoScanEm = agora;
        await _db.SaveChangesAsync(ct);
        await AssetScoreCalculator.RecalcularAsync(_db, asset.CompanyId, asset.Nome);

        var novosCount = primeiraVarredura ? 0 : novos.Count;
        var resolvidosCount = primeiraVarredura ? 0 : resolvidos.Count;

        if (automatico && (novosCount > 0 || resolvidosCount > 0))
        {
            _log.LogInformation("Scan automático de {Dominio}: {Novos} novo(s), {Resolvidos} resolvido(s).",
                asset.Nome, novosCount, resolvidosCount);
        }

        return new Resultado(resultado.Achados.Count, novosCount, resolvidosCount, true);
    }
}
