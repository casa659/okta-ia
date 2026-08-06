using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;

namespace OktaIA.Web.Services;

// Recalcula Vulns{Criticas,Altas,Medias,Baixas}/Saude/TlsStatus de um Asset real a partir dos
// achados FonteScan=true atuais — usado depois de um scan completo (Ativos) e depois de um
// "Reverificar" que remove um achado individual (Vulnerabilidades), pra não duplicar a fórmula.
public static class AssetScoreCalculator
{
    public static async Task RecalcularAsync(ApplicationDbContext db, int? companyId, string assetNome)
    {
        var asset = await db.Assets.FirstOrDefaultAsync(a => a.CompanyId == companyId && a.Nome == assetNome && a.Real);
        if (asset is null)
        {
            return;
        }

        var achados = await db.Vulnerabilities
            .Where(v => v.CompanyId == companyId && v.AssetNome == assetNome && v.FonteScan)
            .ToListAsync();

        var criticas = achados.Count(a => a.Severidade == Severidade.Critica);
        var altas = achados.Count(a => a.Severidade == Severidade.Alta);
        var medias = achados.Count(a => a.Severidade == Severidade.Media);
        var baixas = achados.Count(a => a.Severidade == Severidade.Baixa);

        asset.VulnsCriticas = criticas;
        asset.VulnsAltas = altas;
        asset.VulnsMedias = medias;
        asset.VulnsBaixas = baixas;
        asset.Saude = Math.Clamp(100 - (criticas * 25 + altas * 12 + medias * 5 + baixas * 2), 0, 100);

        var tlsCritico = achados.Any(a => a.TituloPt is "Certificado TLS expirado" or "Certificado TLS expirando em breve" or "Protocolo TLS desatualizado");
        var falhaTls = achados.Any(a => a.TituloPt == "Falha ao verificar TLS");
        asset.TlsStatus = tlsCritico ? AssetTlsStatus.Critico
            : falhaTls ? AssetTlsStatus.Alerta
            : AssetTlsStatus.Ok;

        await db.SaveChangesAsync();
    }
}
