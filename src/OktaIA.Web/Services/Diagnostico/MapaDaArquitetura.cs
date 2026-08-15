using OktaIA.Web.Models;

namespace OktaIA.Web.Services.Diagnostico;

/// <summary>Como uma camada da arquitetura aparece no mapa.</summary>
public enum EstadoDaCamada
{
    /// <summary>Ninguém respondeu nada aqui. Não é "descoberto" — é "não olhamos".</summary>
    NaoAvaliado,

    /// <summary>Controles ausentes na maior parte. É onde um ataque encontra caminho.</summary>
    Descoberto,

    /// <summary>Existe proteção, mas com furo conhecido.</summary>
    Parcial,

    Protegido,

    /// <summary>A camada não existe no ambiente do cliente (ex.: nuvem em quem não usa nuvem).</summary>
    NaoSeAplica,
}

/// <param name="Codigo">Chave estável da camada.</param>
/// <param name="Nome">Como aparece no desenho.</param>
/// <param name="Estado">Resultado da avaliação.</param>
/// <param name="Cobertura">0 a 100 — proporção do que existe nesta camada.</param>
/// <param name="Respondidas">Perguntas respondidas de <paramref name="Total"/> aplicáveis.</param>
/// <param name="Resumo">Uma frase que explica o estado. É o que o diretor lê.</param>
/// <param name="MaiorLacuna">O controle ausente mais grave desta camada, quando houver.</param>
public record CamadaDaArquitetura(
    string Codigo, string Nome, EstadoDaCamada Estado, int Cobertura,
    int Respondidas, int Total, string Resumo, string? MaiorLacuna);

/// <summary>
/// Traduz o questionário no desenho do ambiente: Internet → Firewall → Rede → Servidores →
/// Endpoints → Nuvem → Aplicações → Identidades → Backup → SIEM/SOC.
///
/// Serve a uma conversa específica, e é por isso que existe separado dos números: sentar com o
/// diretor e mostrar ONDE está o buraco vale mais que dizer que a cobertura é 63%. Um percentual
/// não diz se o problema é o backup ou a borda.
///
/// ⚠️ O mapa herda a honestidade do resto do módulo: camada sem resposta aparece como **não
/// avaliada**, nunca como descoberta. Pintar de vermelho o que ninguém olhou é inventar achado — e
/// é o tipo de exagero que destrói a confiança no documento inteiro quando o cliente percebe.
/// </summary>
public static class MapaDaArquitetura
{
    /// <summary>
    /// Camada → perguntas que a descrevem. O mapeamento é explícito, por código, e não por domínio:
    /// "Internet" e "Aplicações" não correspondem a um domínio do questionário, e algumas perguntas
    /// pesam em camadas diferentes daquela em que foram feitas.
    /// </summary>
    private record Definicao(string Codigo, string Nome, string[] Perguntas, string? CondicaoDominio = null);

    private static readonly Definicao[] Camadas =
    [
        new("internet", "Internet", ["rede.publicados", "vuln.externa", "rede.ddos", "ot.exposicao"]),
        new("firewall", "Firewall", ["rede.firewall", "rede.firewall.licenca", "rede.firewall.firmware",
            "rede.firewall.regras", "rede.firewall.backupconfig", "rede.ips"]),
        new("rede", "Rede", ["rede.segmentacao", "rede.vpn", "rede.wifi", "rede.dns", "rede.logs", "ot.segmentacao"]),
        new("servidores", "Servidores", ["infra.inventario", "infra.eol", "infra.hardening",
            "infra.acessofisico", "infra.energia"]),
        new("endpoints", "Endpoints", ["endpoint.protecao", "endpoint.edr", "endpoint.cobertura",
            "endpoint.alertas", "endpoint.ransomware", "endpoint.patch", "endpoint.criptografia",
            "endpoint.usb", "endpoint.admin", "endpoint.mdm"]),
        new("cloud", "Nuvem", ["cloud.mfa", "cloud.exposicao", "cloud.logs", "cloud.cspm",
            "cloud.segredos", "cloud.backup"], CondicaoDominio: "cloud.usa"),
        new("aplicacoes", "Aplicações", ["dev.ambientes", "dev.segredos", "dev.sast", "dev.dependencias",
            "dev.revisao", "dev.dadosprod", "vuln.gestao", "vuln.sla", "vuln.pentest"]),
        new("identidades", "Identidades", ["identidade.diretorio", "identidade.mfa", "identidade.mfa.cobertura",
            "identidade.privilegiadas", "identidade.pam", "identidade.desligados", "identidade.revisao",
            "identidade.senhas", "identidade.terceiros", "email.antiphishing", "email.dmarc"]),
        new("backup", "Backup", ["backup.existe", "backup.offline", "backup.imutavel", "backup.externo",
            "backup.teste", "backup.dr"]),
        new("monitoramento", "SIEM / SOC", ["mon.siem", "mon.fontes", "mon.retencao", "mon.soc",
            "mon.regras", "mon.ti", "resp.plano"]),
    ];

    public static List<CamadaDaArquitetura> Montar(Models.Diagnostico diagnostico)
    {
        var escolhas = diagnostico.Respostas.ToDictionary(r => r.PerguntaCodigo, r => r.Opcao);
        var porCodigo = diagnostico.Respostas.ToDictionary(r => r.PerguntaCodigo);
        var mapa = new List<CamadaDaArquitetura>();

        foreach (var def in Camadas)
        {
            // Camada que depende de o cliente usar aquilo (nuvem) some quando ele não usa. Contá-la
            // como lacuna puniria a empresa por não ter um problema.
            if (def.CondicaoDominio is { } gate)
            {
                var usa = escolhas.GetValueOrDefault(gate);
                if (usa is CatalogoDeDominios.Nao)
                {
                    mapa.Add(new CamadaDaArquitetura(def.Codigo, def.Nome, EstadoDaCamada.NaoSeAplica,
                        0, 0, 0, "A empresa não utiliza este recurso.", null));
                    continue;
                }
            }

            decimal peso = 0, nota = 0;
            int respondidas = 0, aplicaveis = 0;
            PerguntaDoDiagnostico? piorLacuna = null;
            var piorGravidade = -1;

            foreach (var codigo in def.Perguntas)
            {
                var pergunta = CatalogoDeDominios.BuscarPergunta(codigo);
                if (pergunta is null) { continue; }

                var dominio = CatalogoDeDominios.DominioDaPergunta(codigo);
                if (dominio is not null && !CalculadoraDoDiagnostico.Visivel(dominio.SomenteSe, escolhas)) { continue; }
                if (!CalculadoraDoDiagnostico.Visivel(pergunta.SomenteSe, escolhas)) { continue; }

                aplicaveis++;

                if (!porCodigo.TryGetValue(codigo, out var resposta) || resposta.Opcao is null) { continue; }
                if (resposta.Origem == OrigemDaInformacao.NaoAplicavel) { aplicaveis--; continue; }

                respondidas++;

                var bruta = resposta.Opcao switch
                {
                    CatalogoDeDominios.Sim => 1m,
                    CatalogoDeDominios.Parcial => 0.5m,
                    _ => 0m,
                };
                if (pergunta.RespostaBoaEhNao) { bruta = 1m - bruta; }

                var p = Math.Max(pergunta.Peso, 1);
                peso += p;
                nota += bruta * p;

                if (bruta < 1m && pergunta.RiscoSeNao is { } g && (int)g > piorGravidade)
                {
                    piorGravidade = (int)g;
                    piorLacuna = pergunta;
                }
            }

            if (respondidas == 0)
            {
                mapa.Add(new CamadaDaArquitetura(def.Codigo, def.Nome, EstadoDaCamada.NaoAvaliado,
                    0, 0, aplicaveis, "Não avaliado neste levantamento.", null));
                continue;
            }

            var cobertura = peso == 0 ? 0 : (int)Math.Round(100 * nota / peso);
            var estado = cobertura >= 80 ? EstadoDaCamada.Protegido
                : cobertura >= 40 ? EstadoDaCamada.Parcial
                : EstadoDaCamada.Descoberto;

            var resumo = estado switch
            {
                EstadoDaCamada.Protegido => "Controles esperados presentes.",
                EstadoDaCamada.Parcial => "Há proteção, mas com furo conhecido.",
                _ => "A maior parte dos controles não existe.",
            };

            // Levantamento pela metade não vira veredito: a frase precisa dizer que a leitura é
            // parcial, senão o mapa afirma mais do que o dado sustenta.
            if (respondidas < aplicaveis)
            {
                resumo += $" Avaliação parcial ({respondidas} de {aplicaveis} pontos verificados).";
            }

            mapa.Add(new CamadaDaArquitetura(def.Codigo, def.Nome, estado, cobertura,
                respondidas, aplicaveis, resumo, piorLacuna?.Texto));
        }

        return mapa;
    }

    /// <summary>
    /// A frase de abertura da conversa com a diretoria, montada só com o que o levantamento
    /// sustenta. Sem número inventado e sem adjetivo que o dado não comporte.
    /// </summary>
    public static string Narrativa(List<CamadaDaArquitetura> mapa, ResultadoDoDiagnostico resultado, int ferramentas)
    {
        var descobertas = mapa.Count(c => c.Estado == EstadoDaCamada.Descoberto);
        var parciais = mapa.Count(c => c.Estado == EstadoDaCamada.Parcial);
        var naoAvaliadas = mapa.Count(c => c.Estado == EstadoDaCamada.NaoAvaliado);

        var partes = new List<string>();

        if (ferramentas > 0)
        {
            partes.Add($"A empresa já investiu em {ferramentas} tecnologia{(ferramentas > 1 ? "s" : "")} de segurança");
            if (resultado.UsoDoInvestimento is { } uso && uso < 100)
            {
                partes[^1] += $", das quais usa efetivamente {uso}% das capacidades avaliadas";
            }
            partes[^1] += ".";
        }

        var achados = new List<string>();
        if (descobertas > 0) { achados.Add($"{descobertas} camada{(descobertas > 1 ? "s" : "")} descoberta{(descobertas > 1 ? "s" : "")}"); }
        if (parciais > 0) { achados.Add($"{parciais} parcialmente coberta{(parciais > 1 ? "s" : "")}"); }
        if (achados.Count > 0)
        {
            partes.Add($"No desenho do ambiente encontramos {string.Join(" e ", achados)}.");
        }

        if (naoAvaliadas > 0)
        {
            partes.Add(naoAvaliadas == 1
                ? "Outra camada não foi avaliada neste levantamento — não significa que esteja bem, significa que ainda não olhamos."
                : $"Outras {naoAvaliadas} não foram avaliadas neste levantamento — não significa que estejam bem, significa que ainda não olhamos.");
        }

        if (descobertas > 0 || parciais > 0)
        {
            partes.Add("O caminho não é necessariamente comprar mais: é cobrir o que falta e passar a operar o que já existe.");
        }

        return string.Join(" ", partes);
    }

    public static string Cor(EstadoDaCamada estado) => estado switch
    {
        EstadoDaCamada.Protegido => "#00E0A4",
        EstadoDaCamada.Parcial => "#F5D547",
        EstadoDaCamada.Descoberto => "#FF3B5C",
        EstadoDaCamada.NaoSeAplica => "#3E5273",
        _ => "#7A8FAB",
    };

    public static string Rotulo(EstadoDaCamada estado) => estado switch
    {
        EstadoDaCamada.Protegido => "PROTEGIDO",
        EstadoDaCamada.Parcial => "PARCIAL",
        EstadoDaCamada.Descoberto => "DESCOBERTO",
        EstadoDaCamada.NaoSeAplica => "N/A",
        _ => "NÃO AVALIADO",
    };
}
