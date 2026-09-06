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
            ? new OrcamentoMonitoramento { Numero = await ProximoNumeroAsync(), NomeEmpresa = "" }
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

        alvo.EstacoesWindows = Math.Max(0, Entrada.EstacoesWindows);
        alvo.EstacoesOutras = Math.Max(0, Entrada.EstacoesOutras);
        alvo.Servidores = Math.Max(0, Entrada.Servidores);
        alvo.ServidoresExpostos = Math.Clamp(Entrada.ServidoresExpostos, 0, Math.Max(0, Entrada.Servidores));
        alvo.HospedagemDoCliente = Entrada.HospedagemDoCliente;
        alvo.RetencaoDias = Entrada.RetencaoDias < 90 ? 90 : Entrada.RetencaoDias;
        alvo.Cobertura = Entrada.Cobertura;
        alvo.Conformidade = Limpo(Entrada.Conformidade);
        alvo.Observacoes = Limpo(Entrada.Observacoes);

        var conta = _calculadora.Calcular(alvo);
        alvo.MaquinasTotal = conta.MaquinasTotal;
        alvo.ValorImplantacao = conta.ValorImplantacao;
        alvo.ValorMensal = conta.ValorMensal;
        alvo.CustoDiretoMensal = conta.CustoDiretoMensal;
        alvo.MemoriaDeCalculo = conta.Memoria;
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
    /// ORC-2026-000001. Igual ao protocolo das outras telas: ano no meio, sequência por ano.
    ///
    /// ⚠️ Lê o ÚLTIMO do ano em vez de contar linhas: proposta apagada não pode fazer a próxima
    /// reaproveitar um número que já esteve em cima da mesa de alguém.
    /// </summary>
    private async Task<string> ProximoNumeroAsync()
    {
        var prefixo = $"ORC-{DateTime.UtcNow.Year}-";
        var ultimo = await _db.Orcamentos
            .Where(p => p.Numero.StartsWith(prefixo))
            .OrderByDescending(p => p.Numero)
            .Select(p => p.Numero)
            .FirstOrDefaultAsync();

        var n = 1;
        if (ultimo is not null && int.TryParse(ultimo[prefixo.Length..], out var atual)) { n = atual + 1; }
        return prefixo + n.ToString("D6");
    }

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
