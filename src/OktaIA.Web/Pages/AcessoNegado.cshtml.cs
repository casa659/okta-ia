using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OktaIA.Web.Pages;

// Destino de ForbidResult (AreaPermissionFilter) — nunca a própria página de login/dashboard,
// senão um usuário sem permissão nenhuma entraria em loop de redirecionamento (Login manda pro
// destino padrão dele, que é a mesma página bloqueada).
[Authorize]
public class AcessoNegadoModel : PageModel
{
    public void OnGet()
    {
    }
}
