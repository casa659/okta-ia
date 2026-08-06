using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OktaIA.Web.Pages.Admin;

[Authorize]
public class EventosSyncModel : PageModel
{
    public void OnGet()
    {
    }
}
