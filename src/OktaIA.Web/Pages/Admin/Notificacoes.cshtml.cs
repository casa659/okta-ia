using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OktaIA.Web.Pages.Admin;

[Authorize]
public class NotificacoesModel : PageModel
{
    public void OnGet()
    {
    }
}
