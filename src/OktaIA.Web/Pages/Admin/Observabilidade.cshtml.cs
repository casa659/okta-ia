using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OktaIA.Web.Pages.Admin;

[Authorize]
public class ObservabilidadeModel : PageModel
{
    public void OnGet()
    {
    }
}
