using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;
using OktaIA.Web.Services;

namespace OktaIA.Web.Pages.Admin;

/// <summary>
/// Orçamentos de monitoramento gerenciado: o questionário, a conta e o registro.
///
/// ⚠️ O QUESTIONÁRIO É CURTO DE PROPÓSITO. Toda pergunta a mais é uma reunião a mais antes de
/// existir preço — e proposta que demora perde para a que chegou primeiro. Só entrou aqui o que
/// MUDA O NÚMERO: quantas máquinas, quantas expostas, quem hospeda, quanto histórico, que
/// cobertura. O resto (topologia, ferramentas atuais, maturidade) é assunto da implantação, e o
/// L'okta já tem tela própria para isso em Diagnósticos.
/// </summary>
[Authorize]
public class OrcamentosModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly CalculadoraDeOrcamento _calculadora;
    private readonly AdminAuditService _auditoria;

    public OrcamentosModel(ApplicationDbContext db, CalculadoraDeOrcamento calculadora,
        AdminAuditService auditoria)
    {
        _db = db;
        _calculadora = calculadora;
        _auditoria = auditoria;
    }

    [BindProperty(SupportsGet = true)] public int? Id { get; set; }
    [BindProperty(SupportsGet = true)] public bool Nova { get; set; }

    /// <summary>Abre a gaveta de preços já aberta — usado ao voltar de salvá-los.</summary>
    [BindProperty(SupportsGet = true)] public bool Parametros_ { get; set; }

    [BindProperty] public OrcamentoMonitoramento Entrada { get; set; } = new()
    {
        Numero = "",
        NomeEmpresa = "",
    };

    /// <summary>Empresa já cadastrada, para o seletor. Só o que preenche o formulário.</summary>
    public record EmpresaConhecida(int Id, string Nome, string? Cnpj, string? Dominio, int Ativos);

    public List<EmpresaConhecida> Empresas { get; private set; } = [];
    public List<OrcamentoMonitoramento> Lista { get; private set; } = [];
    public OrcamentoMonitoramento? Aberta { get; private set; }
    public CalculadoraDeOrcamento.Resultado? Conta { get; private set; }
    public ParametrosOrcamento Parametros => _calculadora.Parametros;
    public string? Recado { get; private set; }
    public bool RecadoRuim { get; private set; }

    public async Task OnGetAsync()
    {
        await CarregarAsync();
    }

    private async Task CarregarAsync()
    {
        // ⚠️ Só as ATIVAS e não-demo. Empresa de demonstração no seletor faria um orçamento real
        // nascer vinculado a uma empresa que não existe — e o vínculo é o que liga o orçamento ao
        // cliente depois que ele fecha.
        Empresas = await _db.Companies.AsNoTracking()
            .Where(c => c.Ativo && !c.Demo)
            .OrderBy(c => c.Nome)
            .Select(c => new EmpresaConhecida(c.Id, c.Nome, c.Cnpj, c.Dominio, c.AtivosCount))
            .ToListAsync();

        Lista = await _db.Orcamentos.AsNoTracking()
            .OrderByDescending(p => p.CriadaEm)
            .Take(60)
            .ToListAsync();

        if (Id is { } id)
        {
            Aberta = await _db.Orcamentos.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (Aberta is not null)
            {
                Entrada = Aberta;
                // Recalcula para EXIBIR a memória de cálculo mesmo em proposta antiga — mas o que
                // vale são os valores GRAVADOS. Ver OrcamentoMonitoramento: proposta é promessa com
                // data, e não pode mudar porque um parâmetro mudou depois.
                Conta = _calculadora.Calcular(Aberta);
            }
        }

        Recado = TempData["Recado"] as string;
        RecadoRuim = TempData["RecadoRuim"] is true;
    }

    /// <summary>Calcula e grava. O mesmo botão serve para criar e para corrigir.</summary>
    public async Task<IActionResult> OnPostSalvarAsync()
    {
        if (string.IsNullOrWhiteSpace(Entrada.NomeEmpresa))
        {
            TempData["Recado"] = "Diga o nome da empresa — é o que identifica o orçamento.";
            TempData["RecadoRuim"] = true;
            return RedirectToPage(new { id = Id, nova = true });
        }

        var novo = Entrada.Id == 0;
        var alvo = novo
            ? new OrcamentoMonitoramento { Numero = await Services.NumeracaoDeOrcamento.ProximoAsync(_db), NomeEmpresa = "" }
            : await _db.Orcamentos.FirstOrDefaultAsync(p => p.Id == Entrada.Id);

        if (alvo is null) { return RedirectToPage(); }

        // ⚠️ Campo a campo, e não `_db.Update(Entrada)`: o formulário não traz Numero, CriadaEm
        // nem CriadaPor, e um update do objeto inteiro apagaria os três — o registro perderia a
        // identidade que o cliente cita ao telefone.
        // ⚠️ O vínculo com a empresa só existe quando ELA JÁ EXISTE. Prospecto fica sem
        // CompanyId de propósito: criar a empresa no momento do orçamento encheria a lista de
        // clientes de gente que nunca fechou, e o painel passaria a contar prospecto como cliente.
        alvo.CompanyId = Entrada.CompanyId is > 0 ? Entrada.CompanyId : null;
        alvo.NomeEmpresa = Entrada.NomeEmpresa.Trim();
        alvo.Cnpj = Limpo(Entrada.Cnpj);
        alvo.Contato = Limpo(Entrada.Contato);
        alvo.Email = Limpo(Entrada.Email);
        alvo.Telefone = Limpo(Entrada.Telefone);
        alvo.ParceiroNome = Limpo(Entrada.ParceiroNome);

        alvo.JaTemFerramenta = Entrada.JaTemFerramenta;
        alvo.FerramentaExistente = Entrada.JaTemFerramenta ? Limpo(Entrada.FerramentaExistente) : null;
        alvo.EstacoesWindows = Math.Max(0, Entrada.EstacoesWindows);
        alvo.EstacoesOutras = Math.Max(0, Entrada.EstacoesOutras);
        alvo.Servidores = Math.Max(0, Entrada.Servidores);
        alvo.ServidoresExpostos = Math.Clamp(Entrada.ServidoresExpostos, 0, Math.Max(0, Entrada.Servidores));
        alvo.HospedagemDoCliente = Entrada.HospedagemDoCliente;
        alvo.RetencaoDias = Entrada.RetencaoDias < 90 ? 90 : Entrada.RetencaoDias;
        alvo.Cobertura = Entrada.Cobertura;
        alvo.TrataDadosDeCriancas = Entrada.TrataDadosDeCriancas;
        alvo.Conformidade = Limpo(Entrada.Conformidade);
        alvo.Observacoes = Limpo(Entrada.Observacoes);

        var conta = _calculadora.Calcular(alvo);
        alvo.MaquinasTotal = conta.MaquinasTotal;
        alvo.CustoDiretoMensal = conta.CustoDiretoMensal;

        // O que a conta deu fica guardado sempre — é a referência da comparação.
        alvo.ValorImplantacaoCalculado = conta.ValorImplantacao;
        alvo.ValorMensalCalculado = conta.ValorMensal;

        // ⚠️ O VALOR DE MÃO GANHA DA CONTA. A conta é sugestão; quem fecha o negócio é gente.
        // Zero é tratado como "sem valor de mão": um campo em branco vira 0 no binder, e aceitar
        // isso faria um formulário salvo sem querer zerar a proposta.
        alvo.ValorImplantacaoManual = Entrada.ValorImplantacaoManual is > 0 ? Entrada.ValorImplantacaoManual : null;
        alvo.ValorMensalManual = Entrada.ValorMensalManual is > 0 ? Entrada.ValorMensalManual : null;

        alvo.ValorImplantacao = alvo.ValorImplantacaoManual ?? conta.ValorImplantacao;
        alvo.ValorMensal = alvo.ValorMensalManual ?? conta.ValorMensal;

        var memoria = conta.Memoria;
        if (alvo.ValorImplantacaoManual is { } vi)
        {
            memoria += $"\n⚠️ Implantação definida à mão: R$ {vi:N2} (a conta deu R$ {conta.ValorImplantacao:N2}).";
        }
        if (alvo.ValorMensalManual is { } vm)
        {
            memoria += $"\n⚠️ Mensalidade definida à mão: R$ {vm:N2} (a conta deu R$ {conta.ValorMensal:N2}).";
        }
        alvo.MemoriaDeCalculo = memoria;
        alvo.ValidaAte = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(_calculadora.Parametros.ValidadeDias));

        if (novo)
        {
            alvo.CriadaPor = User.Identity?.Name;
            _db.Orcamentos.Add(alvo);
        }

        await _db.SaveChangesAsync();
        await _auditoria.RegistrarAsync(novo ? "Orçamento criado" : "Orçamento atualizado",
            $"{alvo.Numero} · {alvo.NomeEmpresa} · {alvo.MaquinasTotal} máquina(s)",
            User.Identity?.Name ?? "sistema");

        TempData["Recado"] = novo
            ? $"Orçamento {alvo.Numero} criado."
            : $"Orçamento {alvo.Numero} atualizado.";
        return RedirectToPage(new { id = alvo.Id });
    }

    /// <summary>Marca o que aconteceu com a proposta. Só isso — nada é apagado.</summary>
    public async Task<IActionResult> OnPostStatusAsync(int id, StatusOrcamento status)
    {
        var p = await _db.Orcamentos.FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) { return RedirectToPage(); }

        p.Status = status;
        if (status == StatusOrcamento.Enviada) { p.EnviadaEm = DateTimeOffset.UtcNow; }
        if (status is StatusOrcamento.Aceita or StatusOrcamento.Recusada) { p.RespondidaEm = DateTimeOffset.UtcNow; }

        await _db.SaveChangesAsync();
        await _auditoria.RegistrarAsync("Orçamento mudou de status",
            $"{p.Numero} · {p.NomeEmpresa} · {status}", User.Identity?.Name ?? "sistema");

        TempData["Recado"] = $"Orçamento {p.Numero}: {status}.";
        return RedirectToPage(new { id });
    }

    /// <summary>
    /// A via do cliente, em PDF.
    ///
    /// ⚠️ Sai do que está GRAVADO, não do que está na tela. Quem alterou o formulário e não
    /// clicou em "Calcular e salvar" receberia um PDF com valores que o banco não conhece — e
    /// esse PDF vai para o cliente.
    /// </summary>
    public async Task<IActionResult> OnGetPdfAsync(int id, [FromServices] OrcamentoPdfService pdf)
    {
        var o = await _db.Orcamentos.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (o is null) { return RedirectToPage(); }

        var bytes = pdf.Gerar(o);
        return File(bytes, "application/pdf", $"{o.Numero} - {Arquivo(o.NomeEmpresa)}.pdf");
    }

    /// <summary>
    /// Grava os preços. É o dono mexendo no próprio preço — por isso vai para a auditoria com o
    /// nome de quem mexeu.
    ///
    /// ⚠️ NÃO recalcula os orçamentos existentes, e é de propósito: orçamento é promessa com
    /// data. O que o cliente recebeu ontem continua valendo pelo que dizia.
    /// </summary>
    public async Task<IActionResult> OnPostParametrosAsync(ParametrosOrcamento parametros)
    {
        var atual = _calculadora.Parametros;   // garante a linha existindo

        var alvo = await _db.ParametrosOrcamento.FirstAsync(x => x.Id == 1);
        alvo.MensalBase = Math.Max(0, parametros.MensalBase);
        alvo.MaquinasIncluidas = Math.Max(0, parametros.MaquinasIncluidas);
        alvo.MensalPorMaquinaExtra = Math.Max(0, parametros.MensalPorMaquinaExtra);
        alvo.MensalPorServidorExposto = Math.Max(0, parametros.MensalPorServidorExposto);
        alvo.CustoVpsMensal = Math.Max(0, parametros.CustoVpsMensal);
        alvo.MensalHospedagem = Math.Max(0, parametros.MensalHospedagem);
        alvo.ImplantacaoBase = Math.Max(0, parametros.ImplantacaoBase);
        alvo.ImplantacaoPorMaquina = Math.Max(0, parametros.ImplantacaoPorMaquina);
        alvo.MensalPor90DiasExtras = Math.Max(0, parametros.MensalPor90DiasExtras);

        // Fator abaixo de 1 daria desconto por ampliar a cobertura — o contrário do que ela é.
        alvo.FatorEstendida = parametros.FatorEstendida < 1m ? 1m : parametros.FatorEstendida;
        alvo.Fator24x7 = parametros.Fator24x7 < 1m ? 1m : parametros.Fator24x7;
        alvo.ValidadeDias = Math.Clamp(parametros.ValidadeDias, 1, 365);

        alvo.AtualizadoEm = DateTimeOffset.UtcNow;
        alvo.AtualizadoPor = User.Identity?.Name;

        await _db.SaveChangesAsync();
        await _auditoria.RegistrarAsync("Preços do orçamento alterados",
            $"base R$ {alvo.MensalBase:N2} · máquina extra R$ {alvo.MensalPorMaquinaExtra:N2} · "
            + $"implantação R$ {alvo.ImplantacaoBase:N2}", User.Identity?.Name ?? "sistema");

        TempData["Recado"] = "Preços atualizados. Orçamentos já emitidos não mudam — cada um guarda o valor com que saiu.";
        return RedirectToPage(new { id = Id, parametros_ = true });
    }

    private static string Arquivo(string nome)
    {
        var limpo = new string(nome.Where(c => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_').ToArray());
        return string.IsNullOrWhiteSpace(limpo) ? "orcamento" : limpo.Trim();
    }

    /// <summary>
    /// ORC-2026-000001. Igual ao protocolo das outras telas: ano no meio, sequência por ano.
    ///
    /// ⚠️ Lê o ÚLTIMO do ano em vez de contar linhas: proposta apagada não pode fazer a próxima
    /// reaproveitar um número que já esteve em cima da mesa de alguém.
    /// </summary>
    private static string? Limpo(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public static string RotuloStatus(StatusOrcamento s) => s switch
    {
        StatusOrcamento.Rascunho => "rascunho",
        StatusOrcamento.Enviada => "enviada",
        StatusOrcamento.Aceita => "aceita",
        StatusOrcamento.Recusada => "recusada",
        _ => s.ToString(),
    };

    public static string CorStatus(StatusOrcamento s) => s switch
    {
        StatusOrcamento.Aceita => "#17A05A",
        StatusOrcamento.Recusada => "#FF3B5C",
        StatusOrcamento.Enviada => "#3D7BFF",
        _ => "#8A96AB",
    };
}
