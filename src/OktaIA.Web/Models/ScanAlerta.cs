namespace OktaIA.Web.Models;

public enum TipoMudancaScan
{
    Novo = 1,       // achado que não existia na varredura anterior
    Resolvido = 2,  // achado que existia e deixou de aparecer
}

// Mudança detectada entre duas varreduras do mesmo ativo.
//
// É o que diferencia monitoramento de laudo pontual: o scan sozinho responde "como está agora",
// enquanto o histórico de mudanças responde "o que mudou desde ontem" — que é a pergunta que
// justifica acompanhamento recorrente. Sem isso, o produto entrega uma foto; com isso, entrega
// vigilância.
//
// Gravado tanto pelo scan automático (ScanAgendadorService) quanto pelo manual, porque os dois
// passam pelo mesmo ScanExecutor.
public class ScanAlerta
{
    public int Id { get; set; }

    public int? CompanyId { get; set; }
    public Company? Company { get; set; }

    public required string AssetNome { get; set; }

    public TipoMudancaScan Tipo { get; set; }

    public required string TituloPt { get; set; }
    public required string TituloEn { get; set; }

    public Severidade Severidade { get; set; }
    public string? CategoriaScan { get; set; }

    public DateTimeOffset DetectadoEm { get; set; } = DateTimeOffset.UtcNow;

    // Varredura automática do agendador (true) ou clique manual em "Escanear agora" (false).
    // Serve pra distinguir, no histórico, o que a plataforma pegou sozinha do que alguém foi
    // olhar — é a evidência de que o monitoramento contínuo está de fato funcionando.
    public bool Automatico { get; set; }
}
