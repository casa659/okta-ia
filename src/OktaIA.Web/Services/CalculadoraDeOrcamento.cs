using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Models;

namespace OktaIA.Web.Services;

/// <summary>
/// Os números que decidem o preço. **Uma linha no banco**, editável na própria tela.
///
/// ⚠️ ESTES VALORES SÃO DO DONO, NÃO DO CÓDIGO. Nenhum deles saiu de pesquisa de mercado, e
/// apresentá-los como "o preço certo" seria dar a um chute a aparência de dado.
///
/// ⚠️ POR QUE SAIU DA CONFIGURAÇÃO PARA O BANCO (06/09/2026, pedido do dono: "permitir que o adm
/// consiga alterar os preços"): App Setting muda preço com reinício do site e acesso ao Azure —
/// ou seja, na prática só eu mudaria. Preço é decisão comercial e muda no meio de uma negociação;
/// tem de estar a um clique de quem vende. A configuração `Propostas:*` continua valendo como
/// SEMENTE: é dela que a primeira linha nasce, e depois disso o banco manda.
///
/// O que se sabe com precisão é o CUSTO: um VPS de 8 GB que aguenta o Wazuh de um cliente sai por
/// R$ 44/mês (Hostinger KVM 2, medido em 06/09/2026). O resto é decisão comercial.
/// </summary>
public class ParametrosOrcamento
{
    /// <summary>Sempre 1: existe uma tabela de parâmetros com uma linha só.</summary>
    public int Id { get; set; } = 1;

    /// <summary>Quem mexeu por último, e quando. Preço que muda sem rastro vira discussão.</summary>
    public DateTimeOffset? AtualizadoEm { get; set; }
    public string? AtualizadoPor { get; set; }

    /// <summary>Base da mensalidade, antes de contar máquina. Cobre a operação existir.</summary>
    public decimal MensalBase { get; set; } = 600m;

    /// <summary>Quantas máquinas já estão dentro da base.</summary>
    public int MaquinasIncluidas { get; set; } = 10;

    /// <summary>Mensalidade por máquina acima do teto do pacote.</summary>
    public decimal MensalPorMaquinaExtra { get; set; } = 90m;

    /// <summary>
    /// Acréscimo mensal por servidor exposto à internet.
    ///
    /// ⚠️ Não é ganância: servidor exposto gera tentativa de invasão todo dia, e é o que enche a
    /// fila de quem atende. Cobrar igual por dentro e por fora faz o cliente com dez servidores
    /// públicos custar o mesmo que o com nenhum.
    /// </summary>
    public decimal MensalPorServidorExposto { get; set; } = 120m;

    /// <summary>Custo real do VPS de um cliente, quando somos nós que hospedamos.</summary>
    public decimal CustoVpsMensal { get; set; } = 44m;

    /// <summary>O que se cobra pelo VPS quando o hospedamos. Inclui gerenciar, atualizar e renovar.</summary>
    public decimal MensalHospedagem { get; set; } = 150m;

    /// <summary>Base da implantação: levantamento, servidor, certificado e o relatório do dia um.</summary>
    public decimal ImplantacaoBase { get; set; } = 1800m;

    /// <summary>Por máquina, na implantação — o agente e a conferência de cada uma.</summary>
    public decimal ImplantacaoPorMaquina { get; set; } = 90m;

    /// <summary>Multiplicador da mensalidade por cobertura. Comercial é a referência: 1,0.</summary>
    public decimal FatorEstendida { get; set; } = 1.4m;

    /// <summary>⚠️ Só use com plantão de verdade por trás. Promessa de 24×7 sem plantão quebra no primeiro incidente noturno.</summary>
    public decimal Fator24x7 { get; set; } = 2.2m;

    /// <summary>Acréscimo mensal por cada 90 dias de retenção além dos 90 do padrão (mais disco).</summary>
    public decimal MensalPor90DiasExtras { get; set; } = 80m;

    /// <summary>Por quantos dias a proposta vale.</summary>
    public int ValidadeDias { get; set; } = 30;
}

/// <summary>
/// Transforma o levantamento em dois números — implantação e mensalidade — e explica como chegou lá.
///
/// ⚠️ A MEMÓRIA DE CÁLCULO NÃO É ENFEITE. Uma proposta que mostra só o total obriga quem vende a
/// defender um número que ele mesmo não sabe explicar — e, na primeira negociação, o desconto sai
/// do lugar errado. Com as parcelas à vista, dá para dizer "tiro a hospedagem" em vez de "faço por
/// menos".
/// </summary>
public class CalculadoraDeOrcamento
{
    private readonly OktaIA.Web.Data.ApplicationDbContext _db;
    private readonly ParametrosOrcamento _semente;
    private ParametrosOrcamento? _cache;

    public CalculadoraDeOrcamento(OktaIA.Web.Data.ApplicationDbContext db,
        Microsoft.Extensions.Options.IOptions<ParametrosOrcamento> semente)
    {
        _db = db;
        _semente = semente.Value;
    }

    /// <summary>
    /// Os parâmetros em vigor. Vêm do banco; na primeira vez, nascem da configuração.
    ///
    /// ⚠️ Semeia e GRAVA em vez de só devolver os padrões: sem a linha no banco, a tela de edição
    /// não teria o que editar, e o dono clicaria em salvar num formulário que não existe do outro
    /// lado.
    /// </summary>
    public ParametrosOrcamento Parametros
    {
        get
        {
            if (_cache is not null) { return _cache; }

            _cache = _db.ParametrosOrcamento.FirstOrDefault(x => x.Id == 1);
            if (_cache is null)
            {
                _semente.Id = 1;
                _db.ParametrosOrcamento.Add(_semente);
                _db.SaveChanges();
                _cache = _semente;
            }
            return _cache;
        }
    }

    private ParametrosOrcamento _p => Parametros;

    public record Resultado(
        int MaquinasTotal,
        decimal ValorImplantacao,
        decimal ValorMensal,
        decimal CustoDiretoMensal,
        string Memoria);

    public Resultado Calcular(OrcamentoMonitoramento p)
    {
        var maquinas = p.EstacoesWindows + p.EstacoesOutras + p.Servidores;
        var linhas = new List<string>();

        // ── Implantação ─────────────────────────────────────────────────────────────────────
        var implantacao = _p.ImplantacaoBase + (_p.ImplantacaoPorMaquina * maquinas);
        linhas.Add($"Implantação: base R$ {_p.ImplantacaoBase:N2} + {maquinas} máquina(s) × R$ {_p.ImplantacaoPorMaquina:N2}");

        // ── Mensalidade ─────────────────────────────────────────────────────────────────────
        var mensal = _p.MensalBase;
        linhas.Add($"Mensalidade: base R$ {_p.MensalBase:N2} (até {_p.MaquinasIncluidas} máquinas)");

        var extras = Math.Max(0, maquinas - _p.MaquinasIncluidas);
        if (extras > 0)
        {
            var v = extras * _p.MensalPorMaquinaExtra;
            mensal += v;
            linhas.Add($"+ {extras} máquina(s) além do pacote × R$ {_p.MensalPorMaquinaExtra:N2} = R$ {v:N2}");
        }

        if (p.ServidoresExpostos > 0)
        {
            var v = p.ServidoresExpostos * _p.MensalPorServidorExposto;
            mensal += v;
            linhas.Add($"+ {p.ServidoresExpostos} servidor(es) exposto(s) à internet × R$ {_p.MensalPorServidorExposto:N2} = R$ {v:N2}");
        }

        // ── Hospedagem ──────────────────────────────────────────────────────────────────────
        decimal custoDireto = 0m;
        if (p.HospedagemDoCliente)
        {
            linhas.Add("Hospedagem: por conta do cliente — sem custo e sem cobrança.");
        }
        else
        {
            mensal += _p.MensalHospedagem;
            custoDireto = _p.CustoVpsMensal;
            linhas.Add($"+ Servidor gerenciado (Wazuh dedicado): R$ {_p.MensalHospedagem:N2}");
        }

        // ── Retenção ────────────────────────────────────────────────────────────────────────
        // Só o que passa dos 90 dias do padrão custa: mais histórico é mais disco.
        var blocos = Math.Max(0, (p.RetencaoDias - 90) / 90);
        if (blocos > 0)
        {
            var v = blocos * _p.MensalPor90DiasExtras;
            mensal += v;
            linhas.Add($"+ Retenção de {p.RetencaoDias} dias ({blocos} bloco(s) de 90 além do padrão) = R$ {v:N2}");
        }

        // ── Cobertura ───────────────────────────────────────────────────────────────────────
        // ⚠️ Multiplica o TOTAL, e não uma parcela: cobertura ampliada muda o custo de tudo —
        // plantão, tempo de resposta, quem atende de madrugada.
        var fator = p.Cobertura switch
        {
            CoberturaOrcamento.Estendida => _p.FatorEstendida,
            CoberturaOrcamento.VinteQuatroPorSete => _p.Fator24x7,
            _ => 1m,
        };

        if (fator != 1m)
        {
            var antes = mensal;
            mensal *= fator;
            linhas.Add($"× Cobertura {Rotulo(p.Cobertura)}: fator {fator:N1} sobre R$ {antes:N2}");
        }
        else
        {
            linhas.Add("Cobertura: horário comercial (referência, sem fator).");
        }

        return new Resultado(
            maquinas,
            Math.Round(implantacao, 2),
            Math.Round(mensal, 2),
            custoDireto,
            string.Join("\n", linhas));
    }

    public static string Rotulo(CoberturaOrcamento c) => c switch
    {
        CoberturaOrcamento.Comercial => "horário comercial",
        CoberturaOrcamento.Estendida => "estendida (12 h, todo dia)",
        CoberturaOrcamento.VinteQuatroPorSete => "24 × 7",
        _ => c.ToString(),
    };
}
