namespace OktaIA.Web.Models;

public enum AssetTlsStatus { Ok, Alerta, Critico, NaoAplicavel }

// Site/API/servidor monitorado — vinculado a uma Company (MSSP gerencia o inventário de todas
// as empresas). A tela /Ativos mostra a lista consolidada, igual ao mockup original.
public class Asset
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public Company? Company { get; set; }

    public required string Nome { get; set; }
    public required string Ip { get; set; }
    public required string Tipo { get; set; } // API, WEB, VPN, DB, MAIL, CI, FW
    public required string Stack { get; set; }
    public decimal UptimePercentual { get; set; }

    public int? TlsDias { get; set; } // null = não aplicável (ex.: VPN/DB/FW sem certificado público)
    public AssetTlsStatus TlsStatus { get; set; }

    public int VulnsBaixas { get; set; }
    public int VulnsMedias { get; set; }
    public int VulnsAltas { get; set; }
    public int VulnsCriticas { get; set; }

    public int Saude { get; set; } // 0-100

    // Ativo real (não-seed) cadastrado via "+ Adicionar ativo real" em /Ativos — só esses mostram
    // qualquer controle de autorização/scan na UI. Ativos do seed (nomes tipo api.grupovector.com)
    // podem coincidentemente ser domínios reais de terceiros; nunca escaneáveis.
    public bool Real { get; set; }
    public bool AutorizadoParaScan { get; set; }
    public DateTimeOffset? AutorizadoEm { get; set; }
    public DateTimeOffset? UltimoScanEm { get; set; }
}
