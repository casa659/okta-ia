using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;

namespace OktaIA.Web.Services;

/// <summary>
/// Traduz o que a plataforma MEDIU numa empresa para a linguagem das obrigações da LGPD.
///
/// POR QUE EXISTE: o dado técnico não convence um diretor, e o texto da lei não prova nada
/// sozinho. Quem decide a compra — e quem responde por um incidente — precisa ver os dois lados
/// da mesma linha: o que o art. 46 exige e o que a máquina dele mostrou hoje.
///
/// ⚠️ CADA INDICADOR SAI DE UMA MEDIÇÃO, NUNCA DE UMA DECLARAÇÃO. "A empresa tem monitoramento"
/// é opinião; "o conector sincronizou há 12 minutos e viu 3 máquinas" é fato. Num produto de
/// conformidade, a diferença entre os dois é o produto.
///
/// ⚠️ E O QUE NÃO É COBERTO SAI JUNTO. Uma tela que só mostra o verde vira propaganda — e, num
/// assunto em que o cliente pode ser multado, propaganda é o pior serviço que se presta a ele.
/// Ver <see cref="Lacunas"/>: elas são parte do resultado, não uma nota de rodapé.
/// </summary>
public class PosturaLgpd
{
    private readonly ApplicationDbContext _db;

    public PosturaLgpd(ApplicationDbContext db) => _db = db;

    /// <summary>Como está um requisito. A cor da tela sai daqui, e o texto do PDF também.</summary>
    public enum Situacao { Atendido, Parcial, NaoAtendido, ForaDoEscopo }

    /// <param name="Artigo">O dispositivo da Lei nº 13.709/2018, citado pelo número.</param>
    /// <param name="Exige">O que a lei manda, em português de gente.</param>
    /// <param name="Medido">O que a plataforma viu — número, data, nome. Nunca adjetivo.</param>
    public record Requisito(string Artigo, string Titulo, string Exige, string Medido, Situacao Como);

    public record Resultado(
        string Empresa,
        DateTimeOffset? UltimoSync,
        string? Conector,
        bool ConectorAtivo,
        int MaquinasVistas,
        int AlertasTotal,
        int AlertasGraves,
        int GravesAbertos,
        int DiasDesdeGraveMaisAntigo,
        int AchadosDeConformidade,
        int MesesComDado,
        List<Requisito> Requisitos,
        List<string> Lacunas)
    {
        /// <summary>Quantos requisitos medidos estão atendidos — o número do alto da tela.</summary>
        public int Atendidos => Requisitos.Count(r => r.Como == Situacao.Atendido);
        public int Avaliados => Requisitos.Count(r => r.Como != Situacao.ForaDoEscopo);
    }

    public async Task<Resultado> DeAsync(int companyId, CancellationToken ct = default)
    {
        var empresa = await _db.Companies.AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => c.Nome)
            .FirstOrDefaultAsync(ct) ?? $"empresa #{companyId}";

        var conector = await _db.Conectores.AsNoTracking()
            .Where(c => c.CompanyId == companyId)
            .OrderByDescending(c => c.UltimoSyncEm)
            .Select(c => new { c.Nome, c.UltimoSyncEm, c.Status })
            .FirstOrDefaultAsync(ct);

        var alertas = await _db.AlertasUnificados.AsNoTracking()
            .Where(a => a.CompanyId == companyId)
            .Select(a => new
            {
                a.Severidade, a.Status, a.OcorridoEm, a.IngeridoEm, a.AtivoNome, a.Titulo,
            })
            .ToListAsync(ct);

        var maquinas = alertas
            .Where(a => !string.IsNullOrWhiteSpace(a.AtivoNome))
            .Select(a => a.AtivoNome!)
            .Distinct()
            .Count();

        var graves = alertas.Where(a => a.Severidade is Severidade.Critica or Severidade.Alta).ToList();
        var gravesAbertos = graves.Count(a => a.Status is not (StatusTriagem.Resolvido or StatusTriagem.FalsoPositivo));

        // ⚠️ A IDADE DO MAIS ANTIGO EM ABERTO É O RELÓGIO DO ART. 48. O prazo de comunicação é de
        // 3 dias úteis a contar do CONHECIMENTO — e a plataforma sabe exatamente quando soube.
        // Um alerta grave parado há uma semana não é um número feio na tela: é um prazo correndo.
        var maisAntigoAberto = graves
            .Where(a => a.Status is not (StatusTriagem.Resolvido or StatusTriagem.FalsoPositivo))
            .OrderBy(a => a.OcorridoEm)
            .Select(a => (DateTimeOffset?)a.OcorridoEm)
            .FirstOrDefault();

        var diasParado = maisAntigoAberto is { } m
            ? Math.Max(0, (int)(DateTimeOffset.UtcNow - m).TotalDays)
            : 0;

        // Achado de CONFORMIDADE: o que veio da auditoria de configuração (benchmark CIS). É o
        // que responde ao art. 49, e é diferente de "alerta de ataque".
        var conformidade = alertas.Count(a =>
            a.Titulo.Contains("CIS ", StringComparison.OrdinalIgnoreCase)
            || a.Titulo.Contains("SCA ", StringComparison.OrdinalIgnoreCase));

        var meses = alertas
            .Select(a => new { a.OcorridoEm.Year, a.OcorridoEm.Month })
            .Distinct()
            .Count();

        var ativo = conector is not null && conector.Status == StatusConector.Ativo;
        var recente = conector?.UltimoSyncEm is { } s && (DateTimeOffset.UtcNow - s).TotalHours < 24;

        var requisitos = new List<Requisito>
        {
            new("Art. 46", "Medidas de segurança",
                "Adotar medidas técnicas aptas a proteger os dados pessoais de acessos não "
                + "autorizados e de destruição, perda, alteração ou difusão.",
                ativo && recente
                    ? $"Monitoramento em operação. Último dado recebido em "
                      + $"{conector!.UltimoSyncEm!.Value.ToLocalTime():dd/MM/yyyy 'às' HH:mm}, de {maquinas} máquina(s)."
                    : ativo
                        ? "Conector ativo, mas sem dado nas últimas 24 h — verificar se os agentes estão reportando."
                        : "Nenhum conector ativo. Não há medida técnica de monitoramento em operação.",
                ativo && recente ? Situacao.Atendido : ativo ? Situacao.Parcial : Situacao.NaoAtendido),

            new("Art. 49", "Sistemas conforme boas práticas",
                "Estruturar os sistemas de tratamento para atender aos requisitos de segurança e "
                + "aos padrões de boas práticas e governança.",
                conformidade > 0
                    ? $"{conformidade} achado(s) de conformidade medidos contra o benchmark CIS, "
                      + "item a item, com o caminho de correção."
                    : "Nenhuma auditoria de configuração registrada no período.",
                conformidade > 0 ? Situacao.Atendido : Situacao.NaoAtendido),

            new("Art. 48", "Comunicação de incidente",
                "Comunicar à ANPD e ao titular incidente que possa acarretar risco relevante, em "
                + "até 3 dias úteis do conhecimento (Resolução CD/ANPD nº 15/2024).",
                gravesAbertos == 0
                    ? graves.Count > 0
                        ? $"Capacidade de detecção comprovada: {graves.Count} alerta(s) grave(s) detectado(s), nenhum em aberto."
                        : "Detecção em operação; nenhum alerta grave no período."
                    : $"{gravesAbertos} alerta(s) grave(s) em aberto. O mais antigo aguarda decisão há {diasParado} dia(s).",
                gravesAbertos == 0 ? Situacao.Atendido : diasParado >= 3 ? Situacao.NaoAtendido : Situacao.Parcial),

            new("Art. 6º, X", "Responsabilização e prestação de contas",
                "Demonstrar a adoção de medidas eficazes e capazes de comprovar a observância da lei.",
                meses > 0
                    ? $"Há registro em {meses} mês(es). O relatório mensal pode ser emitido e arquivado como evidência datada."
                    : "Sem registro no período — não há o que demonstrar.",
                meses > 0 ? Situacao.Atendido : Situacao.NaoAtendido),
        };

        // ⚠️ ESTA LISTA É FIXA E É DE PROPÓSITO. Ela não sai de medição porque a plataforma NÃO
        // MEDE isso — e é exatamente esse o ponto: dizer ao cliente, por escrito, o que continua
        // faltando mesmo com tudo verde na tela acima. Uma tela de conformidade que omite as
        // lacunas é um documento que induz a erro justamente quem confia nele.
        var lacunas = new List<string>
        {
            "Base legal e consentimento para cada finalidade de tratamento (arts. 7º e 8º)",
            "Registro das operações de tratamento — o inventário de dados (art. 37)",
            "Atendimento aos direitos do titular, com prazo (art. 18)",
            "Encarregado (DPO) nomeado e publicado (art. 41)",
            "Política de retenção e eliminação de dados (art. 16)",
            "Contratos com operadores e fornecedores (art. 39)",
            "Relatório de impacto à proteção de dados, quando exigido (art. 38)",
            "Treinamento das pessoas que tratam os dados",
        };

        return new Resultado(
            empresa, conector?.UltimoSyncEm, conector?.Nome, ativo, maquinas,
            alertas.Count, graves.Count, gravesAbertos, diasParado, conformidade, meses,
            requisitos, lacunas);
    }

    public static string Rotulo(Situacao s) => s switch
    {
        Situacao.Atendido => "atendido",
        Situacao.Parcial => "parcial",
        Situacao.NaoAtendido => "não atendido",
        _ => "fora do escopo",
    };

    public static string Cor(Situacao s) => s switch
    {
        Situacao.Atendido => "#17A05A",
        Situacao.Parcial => "#FF8A3D",
        Situacao.NaoAtendido => "#FF3B5C",
        _ => "#8A96AB",
    };
}
