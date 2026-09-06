using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;

namespace OktaIA.Web.Services;

/// <summary>
/// Avisa por WhatsApp quando chega alerta grave de um cliente.
///
/// ⚠️ POR QUE WHATSAPP, E NÃO E-MAIL. O L'okta não tem NENHUM canal de envio — não há SMTP, não
/// há biblioteca de e-mail, e `/Admin/Notificações` é maquete (medido em 06/09/2026). Montar
/// e-mail aqui significaria registrar aplicativo no Microsoft 365 e conviver com o bloqueio de
/// SMTP dos Security Defaults. WhatsApp é o canal que o dono de fato lê, e a ponte já existe e
/// funciona.
///
/// ⚠️ CONSEQUÊNCIA, dita para não ser descoberta depois: a ponte é a do iAgrow
/// (`2.25.156.123`). Dois produtos passam a depender dela — se ela cair, o boletim de cotações
/// para de sair E o L'okta para de avisar, e o segundo é silencioso. Quando o L'okta tiver
/// número próprio, é aqui que se troca: só a configuração muda.
///
/// ⚠️ NÃO ESTOURA E NÃO SEGURA O SYNC. Aviso é acessório; ingerir o alerta é o essencial. Uma
/// ponte fora do ar não pode impedir o alerta de entrar no banco.
/// </summary>
public class AvisoDeAlerta
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _cfg;
    private readonly ILogger<AvisoDeAlerta> _log;

    public AvisoDeAlerta(ApplicationDbContext db, IHttpClientFactory http, IConfiguration cfg,
        ILogger<AvisoDeAlerta> log)
    {
        _db = db;
        _http = http;
        _cfg = cfg;
        _log = log;
    }

    private string? Url => _cfg["Avisos:EvolutionUrl"]?.TrimEnd('/');
    private string? Chave => _cfg["Avisos:EvolutionKey"];
    private string Instancia => _cfg["Avisos:EvolutionInstancia"] ?? "iagrow";
    private string? Destino => _cfg["Avisos:Destino"];

    /// <summary>Quantos alertas cabem numa mensagem antes de virar resumo.</summary>
    private const int DetalharAte = 5;

    public bool Configurado =>
        !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(Chave)
        && !string.IsNullOrWhiteSpace(Destino);

    /// <summary>
    /// Olha o que entrou e ainda não foi avisado, e manda UMA mensagem por empresa.
    ///
    /// ⚠️ UMA MENSAGEM, e não uma por alerta. Uma varredura de conformidade entrega centenas de
    /// achados de uma vez; avisar um por um transformaria o WhatsApp em ruído, e ruído é o que
    /// faz alguém silenciar a conversa — perdendo junto o alerta que importava.
    ///
    /// ⚠️ Só CRÍTICA e ALTA. Média e baixa vêm às centenas e não exigem ninguém agora; quem quer
    /// vê-las abre a tela.
    /// </summary>
    public async Task<int> AvisarPendentesAsync(CancellationToken ct = default)
    {
        if (!Configurado) { return 0; }

        var pendentes = await _db.AlertasUnificados
            .Where(a => a.AvisadoEm == null
                     && (a.Severidade == Severidade.Critica || a.Severidade == Severidade.Alta))
            .OrderBy(a => a.OcorridoEm)
            .Take(500)
            .ToListAsync(ct);

        if (pendentes.Count == 0) { return 0; }

        var nomes = await _db.Companies.AsNoTracking()
            .Where(c => pendentes.Select(p => p.CompanyId).Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Nome, ct);

        var enviados = 0;

        foreach (var grupo in pendentes.GroupBy(a => a.CompanyId))
        {
            var empresa = nomes.GetValueOrDefault(grupo.Key, $"empresa #{grupo.Key}");
            var texto = Montar(empresa, grupo.ToList());

            if (await EnviarAsync(texto, ct))
            {
                // ⚠️ Carimba SÓ o que foi de fato enviado. Marcar antes faria uma falha de rede
                // apagar o aviso para sempre — e ninguém saberia que ele nunca saiu.
                var agora = DateTimeOffset.UtcNow;
                foreach (var a in grupo) { a.AvisadoEm = agora; }
                enviados++;
            }
        }

        if (enviados > 0) { await _db.SaveChangesAsync(ct); }
        return enviados;
    }

    private static string Montar(string empresa, List<AlertaUnificado> alertas)
    {
        var criticos = alertas.Count(a => a.Severidade == Severidade.Critica);
        var altos = alertas.Count - criticos;

        var sb = new StringBuilder();
        sb.Append("🛡️ *L'okta · ").Append(empresa).AppendLine("*");

        var partes = new List<string>();
        if (criticos > 0) { partes.Add($"{criticos} crítico(s)"); }
        if (altos > 0) { partes.Add($"{altos} alto(s)"); }
        sb.Append(string.Join(" e ", partes)).AppendLine(" — alerta novo.");
        sb.AppendLine();

        foreach (var a in alertas.Take(DetalharAte))
        {
            sb.Append(a.Severidade == Severidade.Critica ? "🔴 " : "🟠 ");
            sb.Append(Curto(a.Titulo, 90));
            if (!string.IsNullOrWhiteSpace(a.AtivoNome)) { sb.Append(" · ").Append(a.AtivoNome); }
            sb.AppendLine();
        }

        if (alertas.Count > DetalharAte)
        {
            sb.Append("… e mais ").Append(alertas.Count - DetalharAte).AppendLine(".");
        }

        sb.AppendLine();
        sb.Append("Abrir: https://loktaia.com/Alertas");
        return sb.ToString();
    }

    private static string Curto(string? s, int max)
    {
        s = (s ?? "").Trim();
        return s.Length <= max ? s : s[..max] + "…";
    }

    private async Task<bool> EnviarAsync(string texto, CancellationToken ct)
    {
        try
        {
            var cliente = _http.CreateClient();
            cliente.Timeout = TimeSpan.FromSeconds(30);

            // `System.Text.Json` porque é o que o projeto já usa — o WazuhConnector também.
            // Trazer Newtonsoft só para montar duas chaves seria um pacote a mais por nada.
            var corpo = JsonSerializer.Serialize(new { number = Destino, text = texto });

            using var req = new HttpRequestMessage(HttpMethod.Post,
                $"{Url}/message/sendText/{Instancia}");
            req.Headers.Add("apikey", Chave);
            req.Content = new StringContent(corpo, Encoding.UTF8, "application/json");

            using var resp = await cliente.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) { return true; }

            var erro = await resp.Content.ReadAsStringAsync(ct);
            _log.LogWarning("Aviso por WhatsApp recusado ({Codigo}): {Erro}",
                (int)resp.StatusCode, Curto(erro, 200));
            return false;
        }
        catch (Exception ex)
        {
            // Engole de propósito: ver o comentário da classe. O alerta já está no banco.
            _log.LogWarning(ex, "Aviso por WhatsApp falhou.");
            return false;
        }
    }
}
