using OktaIA.Web.Models;

namespace OktaIA.Web.Services;

// Catálogos de referência extraídos literalmente do mockup (ORIG/TYPES/TARGETS/DCS) — usados
// pra gerar o seed de SecurityEvent com a mesma "personalidade" de dados do design original.
// Não são entidades de banco (são metadados fixos, não histórico de negócio).
public static class ThreatCatalog
{
    public static readonly (string Cc, string Pt, string En, decimal Lat, decimal Lng)[] Origens =
    [
        ("CN", "China", "China", 35, 105),
        ("RU", "Rússia", "Russia", 61, 95),
        ("US", "EUA", "USA", 38, -97),
        ("IN", "Índia", "India", 21, 78),
        ("BR", "Brasil", "Brazil", -12, -51),
        ("VN", "Vietnã", "Vietnam", 15, 108),
        ("IR", "Irã", "Iran", 32, 53),
        ("NG", "Nigéria", "Nigeria", 9, 8),
        ("UA", "Ucrânia", "Ukraine", 49, 31),
        ("NL", "Holanda", "Netherlands", 52, 5),
        ("ID", "Indonésia", "Indonesia", -2, 118),
        ("TR", "Turquia", "Türkiye", 39, 35),
    ];

    public static readonly (string Pt, string En, Severidade Sev)[] Tipos =
    [
        ("Injeção SQL", "SQL Injection", Severidade.Critica),
        ("Força bruta SSH", "Brute Force SSH", Severidade.Alta),
        ("Credential stuffing", "Credential Stuffing", Severidade.Alta),
        ("XSS refletido", "XSS Reflected", Severidade.Media),
        ("Varredura de portas", "Port Scan", Severidade.Baixa),
        ("DDoS L7", "DDoS L7 Flood", Severidade.Critica),
        ("Path traversal", "Path Traversal", Severidade.Alta),
        ("Sondagem Log4Shell", "Log4Shell Probe", Severidade.Critica),
        ("Enumeração WordPress", "WordPress Enum", Severidade.Baixa),
        ("Abuso de taxa de API", "API Rate Abuse", Severidade.Media),
        ("Força bruta RDP", "RDP Brute Force", Severidade.Alta),
        ("Túnel DNS", "DNS Tunneling", Severidade.Alta),
        ("Upload malicioso", "Malicious Upload", Severidade.Critica),
        ("Redirecionamento aberto", "Open Redirect", Severidade.Baixa),
        ("Adulteração de JWT", "JWT Tampering", Severidade.Media),
    ];

    public static readonly string[] Alvos =
    [
        "api.grupovector.com", "portal.hsanta.br", "vpn-sp01", "wp.lojaativa.com.br",
        "srv-db-prod-02", "checkout.pagou.io", "mail.prefdigital.gov.br",
    ];

    // Datacenters próprios (pins verdes fixos no mapa) — não é histórico, é topologia de rede.
    public static readonly (string Codigo, decimal Lat, decimal Lng)[] Datacenters =
    [
        ("SAO1", -23.5m, -46.6m), ("GRU2", -23.4m, -46.5m), ("FRA1", 50.1m, 8.7m),
        ("IAD1", 38.9m, -77.4m), ("SCL1", -33.4m, -70.6m),
    ];
}
