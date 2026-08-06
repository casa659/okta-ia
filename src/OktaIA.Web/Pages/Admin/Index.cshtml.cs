using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OktaIA.Web.Pages.Admin;

[Authorize]
public class IndexModel : PageModel
{
    public void OnGet()
    {
    }
}
