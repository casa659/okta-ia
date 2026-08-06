using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OktaIA.Web.Pages.Admin;

[Authorize]
public class CredenciaisModel : PageModel
{
    public void OnGet()
    {
    }
}
