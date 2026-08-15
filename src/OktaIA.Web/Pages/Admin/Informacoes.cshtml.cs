using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OktaIA.Web.Services.Integracoes;

namespace OktaIA.Web.Pages.Admin;

/// <summary>
/// Manual de implantação de conectores — para quem opera a plataforma, não para o cliente.
/// O documento que vai PARA o cliente é outro (lista o que ele provisiona); aqui está o passo a
/// passo de quem executa, incluindo o que pedir, em que ordem clicar e como ler cada falha.
///
/// Os adaptadores disponíveis vêm do RegistroDeConectores e não de uma lista escrita à mão: assim
/// o manual não envelhece quando um fabricante novo é implementado.
/// </summary>
[Authorize]
public class InformacoesModel : PageModel
{
    private readonly RegistroDeConectores _registro;
    private readonly ProtetorDeCredencial _protetor;

    public InformacoesModel(RegistroDeConectores registro, ProtetorDeCredencial protetor)
    {
        _registro = registro;
        _protetor = protetor;
    }

    public IReadOnlyList<CapacidadesConector> Adaptadores { get; private set; } = [];
    public bool CofreConfigurado { get; private set; }

    public void OnGet()
    {
        Adaptadores = _registro.Disponiveis;
        CofreConfigurado = _protetor.Configurado;
    }
}
