using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;
using OktaIA.Web.Services;
using OktaIA.Web.Services.Integracoes;

namespace OktaIA.Web.Pages.Admin;

/// <summary>
/// Cofre: lista as credenciais guardadas e permite ROTACIONAR sem desinstalar o conector.
///
/// A rotação existe por um motivo concreto: cliente troca senha por política, e até aqui o único
/// caminho era Remover + Instalar — só que remover apaga em cascata todos os alertas já ingeridos.
/// Uma troca de senha rotineira destruiria o histórico. Rotacionar substitui só o segredo; conector,
/// cursor e alertas ficam intactos, e a sincronização seguinte continua de onde parou.
///
/// O segredo nunca é exibido, nem aqui nem em lugar nenhum: a tela mostra só a referência
/// não-sensível (usuário, ou os 4 últimos caracteres). Rotacionar é substituir às cegas, de propósito.
/// </summary>
[Authorize]
public class CredenciaisModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly RegistroDeConectores _registro;
    private readonly ProtetorDeCredencial _protetor;
    private readonly AdminAuditService _auditoria;

    public CredenciaisModel(ApplicationDbContext db, RegistroDeConectores registro,
        ProtetorDeCredencial protetor, AdminAuditService auditoria)
    {
        _db = db;
        _registro = registro;
        _protetor = protetor;
        _auditoria = auditoria;
    }

    public record CredencialView(int ConectorId, string Empresa, string Conector, string Slug,
        string? Referencia, string CriadaEm, string? CriadaPor, string Rotacionada,
        StatusConector Status, string StatusRotulo, string Cor,
        IReadOnlyList<CampoCredencial> Campos);

    public List<CredencialView> Credenciais { get; private set; } = [];
    public bool CofreConfigurado { get; private set; }

    [TempData] public string? Mensagem { get; set; }
    [TempData] public bool MensagemOk { get; set; }

    public async Task OnGetAsync() => await CarregarAsync();

    /// <summary>
    /// Substitui o segredo. NÃO mexe em cursor nem em alerta — é justamente o ponto da rotação.
    /// Admin-only: quem troca credencial de cliente tem que estar registrado na auditoria.
    /// </summary>
    public async Task<IActionResult> OnPostRotacionarAsync(int conectorId)
    {
        if (!User.IsInRole("Admin"))
        {
            return Falhar("Só Admin pode rotacionar credencial.");
        }

        if (!_protetor.Configurado)
        {
            return Falhar("Cofre não configurado — a chave de criptografia não está disponível.");
        }

        var conector = await _db.Conectores.Include(c => c.Credencial)
            .FirstOrDefaultAsync(c => c.Id == conectorId);
        if (conector is null)
        {
            return Falhar("Conector não encontrado.");
        }

        var adaptador = _registro.Resolver(conector.Slug);
        if (adaptador is null)
        {
            return Falhar($"Nenhum adaptador registrado para '{conector.Slug}'.");
        }

        var campos = new Dictionary<string, string>();
        foreach (var campo in adaptador.Capacidades.CamposCredencial)
        {
            var valor = Request.Form[$"cred_{campo.Chave}"].ToString();
            if (string.IsNullOrWhiteSpace(valor))
            {
                return Falhar($"Preencha o campo '{campo.Rotulo}'.");
            }

            campos[campo.Chave] = valor;
        }

        var visivel = adaptador.Capacidades.CamposCredencial.FirstOrDefault(c => !c.Segredo);
        var referencia = visivel is not null
            ? campos[visivel.Chave]
            : ProtetorDeCredencial.Referencia(campos.Values.First());

        if (conector.Credencial is null)
        {
            _db.CredenciaisConector.Add(new CredencialConector
            {
                ConectorId = conector.Id,
                SegredoCifrado = _protetor.Proteger(campos),
                Referencia = referencia,
                CriadoPor = User.Identity?.Name,
            });
        }
        else
        {
            conector.Credencial.SegredoCifrado = _protetor.Proteger(campos);
            conector.Credencial.Referencia = referencia;
            conector.Credencial.RotacionadaEm = DateTimeOffset.UtcNow;
        }

        // Volta a valer como "nunca validada": a credencial nova ainda não provou que funciona, e
        // deixar ATIVO faria o agendador sair usando um segredo não testado contra o fabricante.
        conector.Status = StatusConector.NuncaConectado;
        conector.UltimoErro = null;
        conector.UltimoErroEm = null;

        await _db.SaveChangesAsync();

        await _auditoria.RegistrarAsync("credencial.rotacionada",
            $"{conector.Nome} (empresa {conector.CompanyId}) — nova referência: {referencia}",
            User.Identity?.Name ?? "—");

        Mensagem = $"Credencial de {conector.Nome} substituída. Teste a conexão para reativar.";
        MensagemOk = true;
        return RedirectToPage();
    }

    private IActionResult Falhar(string mensagem)
    {
        Mensagem = mensagem;
        MensagemOk = false;
        return RedirectToPage();
    }

    private async Task CarregarAsync()
    {
        CofreConfigurado = _protetor.Configurado;

        var conectores = await _db.Conectores
            .Include(c => c.Credencial)
            .Include(c => c.Company)
            .OrderBy(c => c.Company!.Nome).ThenBy(c => c.Nome)
            .ToListAsync();

        Credenciais = conectores.Select(c => new CredencialView(
            c.Id,
            c.Company?.Nome ?? "—",
            c.Nome,
            c.Slug,
            c.Credencial?.Referencia,
            (c.Credencial?.CriadoEm ?? c.CriadoEm).ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
            c.Credencial?.CriadoPor ?? c.CriadoPor,
            c.Credencial?.RotacionadaEm is null
                ? "nunca"
                : c.Credencial.RotacionadaEm.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
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
            _registro.Resolver(c.Slug)?.Capacidades.CamposCredencial ?? [])).ToList();
    }
}
