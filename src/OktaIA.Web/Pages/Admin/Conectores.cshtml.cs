using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;
using OktaIA.Web.Services;
using OktaIA.Web.Services.Integracoes;

namespace OktaIA.Web.Pages.Admin;

[Authorize]
public class ConectoresModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly RegistroDeConectores _registro;
    private readonly ProtetorDeCredencial _protetor;
    private readonly MotorDeSync _motor;
    private readonly AdminAuditService _auditoria;

    public ConectoresModel(ApplicationDbContext db, RegistroDeConectores registro,
        ProtetorDeCredencial protetor, MotorDeSync motor, AdminAuditService auditoria)
    {
        _db = db;
        _registro = registro;
        _protetor = protetor;
        _motor = motor;
        _auditoria = auditoria;
    }

    public record ConectorView(int Id, string Nome, string Slug, string Categoria, string Fabricante,
        StatusConector Status, string StatusRotulo, string Cor, string? UrlBase, string? Referencia,
        string UltimoSync, string? UltimoErro, int Alertas, string? Cursor);

    public List<ConectorView> Instalados { get; private set; } = [];
    public List<CapacidadesConector> Disponiveis { get; private set; } = [];
    public List<(int Id, string Nome)> EmpresasDisponiveis { get; private set; } = [];
    public int? EmpresaSelecionadaId { get; private set; }
    public string? EmpresaNome { get; private set; }
    public bool CofreConfigurado { get; private set; }

    [TempData] public string? Mensagem { get; set; }
    [TempData] public bool MensagemOk { get; set; }

    public async Task OnGetAsync(int? empresa) => await CarregarAsync(empresa);

    public async Task<IActionResult> OnPostInstalarAsync(string slug, string? urlBase, int? empresaId)
    {
        var adaptador = _registro.Resolver(slug);
        var empresa = await ResolverEmpresaAsync(empresaId);
        if (adaptador is null || empresa is null)
        {
            return await FalharAsync("Adaptador ou empresa não encontrados.");
        }

        if (!_protetor.Configurado)
        {
            return await FalharAsync("Cofre de credenciais não configurado — falta o App Setting Integracoes:ChaveCriptografia.");
        }

        if (adaptador.Capacidades.ExigeUrlBase && string.IsNullOrWhiteSpace(urlBase))
        {
            return await FalharAsync($"{adaptador.Capacidades.Nome} exige a URL do serviço.");
        }

        // Campos de credencial variam por fabricante, então vêm do form pelo nome declarado em
        // Capacidades em vez de por binding fixo.
        var campos = new Dictionary<string, string>();
        foreach (var campo in adaptador.Capacidades.CamposCredencial)
        {
            var valor = Request.Form[$"cred_{campo.Chave}"].ToString();
            if (string.IsNullOrWhiteSpace(valor))
            {
                return await FalharAsync($"Preencha o campo '{campo.Rotulo}'.");
            }

            campos[campo.Chave] = valor;
        }

        if (await _db.Conectores.AnyAsync(c => c.CompanyId == empresa.Id && c.Slug == slug))
        {
            return await FalharAsync($"{adaptador.Capacidades.Nome} já está instalado nesta empresa.");
        }

        var conector = new Conector
        {
            CompanyId = empresa.Id,
            Slug = adaptador.Capacidades.Slug,
            Nome = adaptador.Capacidades.Nome,
            Categoria = adaptador.Capacidades.Categoria,
            Fabricante = adaptador.Capacidades.Fabricante,
            TipoAuth = adaptador.Capacidades.TipoAuth,
            UrlBase = string.IsNullOrWhiteSpace(urlBase) ? null : urlBase.Trim(),
            CriadoPor = User.Identity?.Name,
        };
        _db.Conectores.Add(conector);
        await _db.SaveChangesAsync();

        // Referência é a dica não-sensível pra UI: o primeiro campo não-segredo, ou os 4 últimos
        // caracteres do segredo. Nunca o segredo inteiro.
        var visivel = adaptador.Capacidades.CamposCredencial.FirstOrDefault(c => !c.Segredo);
        var referencia = visivel is not null
            ? campos[visivel.Chave]
            : ProtetorDeCredencial.Referencia(campos.Values.First());

        _db.CredenciaisConector.Add(new CredencialConector
        {
            ConectorId = conector.Id,
            SegredoCifrado = _protetor.Proteger(campos),
            Referencia = referencia,
            CriadoPor = User.Identity?.Name,
        });
        await _db.SaveChangesAsync();

        await _auditoria.RegistrarAsync("conector.instalado",
            $"{conector.Nome} na empresa {empresa.Nome}", User.Identity?.Name ?? "—");

        Mensagem = $"{conector.Nome} instalado. Teste a conexão antes de sincronizar.";
        MensagemOk = true;
        return RedirectToPage(new { empresa = empresa.Id });
    }

    public async Task<IActionResult> OnPostTestarAsync(int id, int? empresa)
    {
        var (conector, adaptador, ctx, erro) = await PrepararAsync(id);
        if (erro is not null)
        {
            return await FalharAsync(erro, empresa);
        }

        var resultado = await adaptador!.TestarConexaoAsync(ctx!, HttpContext.RequestAborted);

        conector!.UltimoHealthCheckEm = DateTimeOffset.UtcNow;
        conector.LatenciaMs = resultado.LatenciaMs;
        if (!resultado.Ok)
        {
            conector.Status = StatusConector.Erro;
            conector.UltimoErro = resultado.Mensagem;
            conector.UltimoErroEm = DateTimeOffset.UtcNow;
        }
        else if (conector.Status == StatusConector.NuncaConectado || conector.Status == StatusConector.Erro)
        {
            conector.Status = StatusConector.Ativo;
            conector.UltimoErro = null;
            conector.UltimoErroEm = null;
        }

        await _db.SaveChangesAsync();

        Mensagem = $"{conector.Nome}: {resultado.Mensagem}" +
                   (resultado.LatenciaMs is not null ? $" ({resultado.LatenciaMs} ms)" : "");
        MensagemOk = resultado.Ok;
        return RedirectToPage(new { empresa });
    }

    public async Task<IActionResult> OnPostSincronizarAsync(int id, int? empresa)
    {
        var conector = await _db.Conectores.FirstOrDefaultAsync(c => c.Id == id);
        if (conector is null)
        {
            return await FalharAsync("Conector não encontrado.", empresa);
        }

        var resumo = await _motor.ExecutarAsync(id, EscopoSync.Alertas, automatico: false, HttpContext.RequestAborted);

        Mensagem = resumo.Sucesso
            ? $"{conector.Nome}: {resumo.Lidos} alerta(s) lido(s), {resumo.Novos} novo(s)."
            : $"{conector.Nome}: falha — {resumo.Erro}";
        MensagemOk = resumo.Sucesso;
        return RedirectToPage(new { empresa });
    }

    /// <summary>Pausa/retoma. Não revoga credencial — mesmo princípio do monitoramento de ativo.</summary>
    public async Task<IActionResult> OnPostPausarAsync(int id, int? empresa)
    {
        var conector = await _db.Conectores.FirstOrDefaultAsync(c => c.Id == id);
        if (conector is null)
        {
            return await FalharAsync("Conector não encontrado.", empresa);
        }

        conector.Status = conector.Status == StatusConector.Pausado
            ? StatusConector.Ativo
            : StatusConector.Pausado;
        await _db.SaveChangesAsync();

        Mensagem = $"{conector.Nome} agora está {(conector.Status == StatusConector.Pausado ? "pausado" : "ativo")}.";
        MensagemOk = true;
        return RedirectToPage(new { empresa });
    }

    /// <summary>
    /// Remove o conector. Admin-only e destrutivo: leva junto credencial, cursor, execuções e
    /// TODOS os alertas já ingeridos por ele (cascade). Mesmo cuidado da exclusão de ativo.
    /// </summary>
    public async Task<IActionResult> OnPostRemoverAsync(int id, int? empresa)
    {
        if (!User.IsInRole("Admin"))
        {
            return await FalharAsync("Só Admin pode remover conector.", empresa);
        }

        var conector = await _db.Conectores.FirstOrDefaultAsync(c => c.Id == id);
        if (conector is null)
        {
            return await FalharAsync("Conector não encontrado.", empresa);
        }

        var alertas = await _db.AlertasUnificados.CountAsync(a => a.ConectorId == id);
        _db.Conectores.Remove(conector);
        await _db.SaveChangesAsync();

        await _auditoria.RegistrarAsync("conector.removido",
            $"{conector.Nome} (empresa {conector.CompanyId}) — {alertas} alerta(s) removidos junto",
            User.Identity?.Name ?? "—");

        Mensagem = $"{conector.Nome} removido — {alertas} alerta(s) apagados junto.";
        MensagemOk = true;
        return RedirectToPage(new { empresa });
    }

    private async Task<(Conector?, IConnector?, ContextoConector?, string?)> PrepararAsync(int id)
    {
        var conector = await _db.Conectores.Include(c => c.Credencial).FirstOrDefaultAsync(c => c.Id == id);
        if (conector is null)
        {
            return (null, null, null, "Conector não encontrado.");
        }

        var adaptador = _registro.Resolver(conector.Slug);
        if (adaptador is null)
        {
            return (conector, null, null, $"Nenhum adaptador registrado para '{conector.Slug}'.");
        }

        if (conector.Credencial is null)
        {
            return (conector, adaptador, null, "Conector sem credencial.");
        }

        var ctx = new ContextoConector(conector.Id, conector.CompanyId, conector.UrlBase,
            _protetor.Revelar(conector.Credencial.SegredoCifrado));

        return (conector, adaptador, ctx, null);
    }

    private async Task<IActionResult> FalharAsync(string mensagem, int? empresa = null)
    {
        Mensagem = mensagem;
        MensagemOk = false;
        await Task.CompletedTask;
        return RedirectToPage(new { empresa });
    }

    /// <summary>
    /// Empresa escolhida no filtro local tem prioridade sobre o tenant global do cabeçalho — mesmo
    /// padrão de Ativos e Vulnerabilidades. Um MSSP configura conector de 30 clientes sem ficar
    /// trocando a organização inteira a cada um.
    /// </summary>
    // Delegado ao TenantResolver de propósito: conta de cliente é presa à própria empresa e o
    // parâmetro  é descartado. Resolver isso aqui, em cinco cópias, era como o furo
    // sobreviveria à correção do resolvedor.
    private async Task<Company?> ResolverEmpresaAsync(int? empresaParam)
        => await TenantResolver.ResolverComFiltroAsync(HttpContext, _db, empresaParam);

    private async Task CarregarAsync(int? empresaParam = null)
    {
        CofreConfigurado = _protetor.Configurado;

        EmpresasDisponiveis = (await TenantResolver.EmpresasVisiveis(HttpContext, _db).OrderBy(c => c.Nome)
                .Select(c => new { c.Id, c.Nome }).ToListAsync())
            .Select(c => (c.Id, c.Nome)).ToList();

        var empresa = await ResolverEmpresaAsync(empresaParam);
        EmpresaNome = empresa?.Nome;
        EmpresaSelecionadaId = empresa?.Id;

        if (empresa is null)
        {
            Disponiveis = _registro.Disponiveis.ToList();
            return;
        }

        var conectores = await _db.Conectores
            .Include(c => c.Credencial)
            .Where(c => c.CompanyId == empresa.Id)
            .OrderBy(c => c.Nome)
            .ToListAsync();

        var ids = conectores.Select(c => c.Id).ToList();

        var alertasPorConector = (await _db.AlertasUnificados
                .Where(a => ids.Contains(a.ConectorId))
                .GroupBy(a => a.ConectorId)
                .Select(g => new { g.Key, Total = g.Count() })
                .ToListAsync())
            .ToDictionary(x => x.Key, x => x.Total);

        var cursores = (await _db.CursoresSync
                .Where(c => ids.Contains(c.ConectorId) && c.Escopo == EscopoSync.Alertas)
                .Select(c => new { c.ConectorId, c.Valor })
                .ToListAsync())
            .ToDictionary(x => x.ConectorId, x => x.Valor);

        Instalados = conectores.Select(c => new ConectorView(
            c.Id, c.Nome, c.Slug, c.Categoria, c.Fabricante,
            c.Status,
            c.Status switch
            {
                StatusConector.Ativo => "ATIVO",
                StatusConector.Pausado => "PAUSADO",
                StatusConector.Erro => "ERRO",
                _ => "NÃO CONECTADO",
            },
            c.Status switch
            {
                StatusConector.Ativo => "#00E0A4",
                StatusConector.Pausado => "#FFC93C",
                StatusConector.Erro => "#FF3B5C",
                _ => "#4A5D78",
            },
            c.UrlBase,
            c.Credencial?.Referencia,
            c.UltimoSyncEm?.ToLocalTime().ToString("dd/MM HH:mm") ?? "nunca",
            c.UltimoErro,
            alertasPorConector.GetValueOrDefault(c.Id),
            cursores.GetValueOrDefault(c.Id))).ToList();

        var instaladosSlugs = conectores.Select(c => c.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Disponiveis = _registro.Disponiveis.Where(d => !instaladosSlugs.Contains(d.Slug)).ToList();
    }
}
