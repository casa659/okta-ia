using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace OktaIA.Web.Pages.Admin;

[Authorize]
public class SdkModel : PageModel
{
    public void OnGet()
    {
    }
}
