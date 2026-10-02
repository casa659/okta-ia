using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OktaIA.Web.Data;
using OktaIA.Web.Models;
using OktaIA.Web.Services;
using OktaIA.Web.Services.Integracoes;

namespace OktaIA.Web.Pages.Admin;

/// <summary>
/// Onde se gera a chave com que o LinkEscola lê o monitoramento de uma empresa.
///
/// Tela própria, e não um pedaço de /Admin/Conectores, porque o assunto é o INVERSO: lá se
/// configura o que esta plataforma LÊ de fora; aqui se autoriza o que ela ENTREGA para fora. Juntar
/// os dois num lugar só faria "remover" e "revogar" parecerem a mesma coisa — e não são: revogar
/// corta a vista do DPO sem tocar na coleta, remover para a coleta sem avisar ninguém.
/// </summary>
[Authorize]
public class ChaveLgpdModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly ChaveDeLeituraLgpd _chaves;
    private readonly AdminAuditService _auditoria;
    private readonly UserManager<ApplicationUser> _usuarios;

    public ChaveLgpdModel(
        ApplicationDbContext db,
        ChaveDeLeituraLgpd chaves,
        AdminAuditService auditoria,
        UserManager<ApplicationUser> usuarios)
    {
        _db = db;
        _chaves = chaves;
        _auditoria = auditoria;
        _usuarios = usuarios;
    }

    public List<(int Id, string Nome)> EmpresasDisponiveis { get; private set; } = [];
    public Company? Empresa { get; private set; }

    /// <summary>
    /// A chave em claro, logo depois de gerada.
    ///
    /// 🔴 VIVE SÓ NESTA RESPOSTA. Não vai para sessão, nem para TempData com redirect: cada lugar a
    /// mais onde ela passa é um lugar a mais de onde ela pode sair. Quem fechar a página antes de
    /// copiar gera outra — o incômodo é de propósito.
    /// </summary>
    public string? ChaveEmClaro { get; private set; }

    public string? Mensagem { get; private set; }
    public bool Erro { get; private set; }

    public async Task<IActionResult> OnGetAsync(int? empresa, CancellationToken ct)
    {
        await CarregarAsync(empresa, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostGerarAsync(int? empresa, string? senha, CancellationToken ct)
    {
        await CarregarAsync(empresa, ct);
        if (Empresa is null) { return Page(); }

        // 🔴 SENHA ANTES DE GERAR. Gerar é destrutivo de um jeito que não parece: a chave anterior
        // morre na hora e o painel do DPO do outro lado para de atualizar sem erro visível para
        // ele. A mesma trava do "remover conector" (02/10/2026), pela mesma razão.
        if (!await SenhaConfereAsync(senha))
        {
            Erro = true;
            Mensagem = "Senha incorreta. A chave não foi gerada.";
            await _auditoria.RegistrarAsync("chave.lgpd.recusada",
                $"Senha incorreta ao tentar gerar a chave de leitura LGPD da empresa {Empresa.Nome} (#{Empresa.Id}).",
                User.Identity?.Name ?? "?");
            return Page();
        }

        var tinha = Empresa.ChaveLgpdHash is not null;
        ChaveEmClaro = await _chaves.GerarAsync(Empresa, User.Identity?.Name ?? "?", ct);

        // ⚠️ Registra o PREFIXO, nunca a chave. Trilha de auditoria é lida por mais gente do que o
        // banco — gravar o segredo aqui seria vazá-lo no lugar que existe para provar zelo.
        await _auditoria.RegistrarAsync("chave.lgpd.gerada",
            $"Chave de leitura LGPD {(tinha ? "SUBSTITUÍDA" : "criada")} para {Empresa.Nome} (#{Empresa.Id}). Prefixo {Empresa.ChaveLgpdPrefixo}.",
            User.Identity?.Name ?? "?");

        Mensagem = tinha
            ? "Chave nova gerada. A anterior parou de funcionar agora — o DPO precisa colar esta no LinkEscola."
            : "Chave gerada. Copie agora: ela não será exibida de novo.";
        return Page();
    }

    public async Task<IActionResult> OnPostRevogarAsync(int? empresa, string? senha, CancellationToken ct)
    {
        await CarregarAsync(empresa, ct);
        if (Empresa is null) { return Page(); }

        if (!await SenhaConfereAsync(senha))
        {
            Erro = true;
            Mensagem = "Senha incorreta. A chave continua valendo.";
            await _auditoria.RegistrarAsync("chave.lgpd.recusada",
                $"Senha incorreta ao tentar revogar a chave de leitura LGPD da empresa {Empresa.Nome} (#{Empresa.Id}).",
                User.Identity?.Name ?? "?");
            return Page();
        }

        await _chaves.RevogarAsync(Empresa, ct);
        await _auditoria.RegistrarAsync("chave.lgpd.revogada",
            $"Chave de leitura LGPD revogada para {Empresa.Nome} (#{Empresa.Id}).",
            User.Identity?.Name ?? "?");

        Mensagem = "Chave revogada. O LinkEscola desta escola para de ler o monitoramento imediatamente.";
        return Page();
    }

    private async Task<bool> SenhaConfereAsync(string? senha)
    {
        if (string.IsNullOrWhiteSpace(senha)) { return false; }
        var eu = await _usuarios.GetUserAsync(User);
        return eu is not null && await _usuarios.CheckPasswordAsync(eu, senha);
    }

    private async Task CarregarAsync(int? empresa, CancellationToken ct)
    {
        EmpresasDisponiveis = (await TenantResolver.EmpresasVisiveis(HttpContext, _db)
                .OrderBy(c => c.Nome)
                .Select(c => new { c.Id, c.Nome })
                .ToListAsync(ct))
            .Select(c => (c.Id, c.Nome))
            .ToList();

        // ⚠️ Pela lista VISÍVEL, e não por `FindAsync(empresa)`: quem administra uma empresa só não
        // pode gerar chave da outra trocando o número na barra de endereço.
        var escolhida = empresa ?? EmpresasDisponiveis.Select(e => (int?)e.Id).FirstOrDefault();
        if (escolhida is null || !EmpresasDisponiveis.Any(e => e.Id == escolhida)) { return; }

        Empresa = await _db.Companies.FirstOrDefaultAsync(c => c.Id == escolhida, ct);
    }
}
