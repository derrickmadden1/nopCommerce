using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Linq;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Web.Controllers;

public partial class HomeController : BasePublicController
{
    [SaveLastContinueShoppingPage]
    public virtual IActionResult Index()
    {
        var acceptHeaders = Request.GetTypedHeaders().Accept?.OrderByDescending(x => x.Quality ?? 1.0).ToList();
        if (acceptHeaders != null && acceptHeaders.Count > 0)
        {
            var preferred = acceptHeaders.FirstOrDefault(x => 
                x.MediaType.Value == "text/markdown" || 
                x.MediaType.Value == "text/plain" || 
                x.MediaType.Value == "application/json" || 
                x.MediaType.Value == "text/html");

            if (preferred != null)
            {
                if (preferred.MediaType.Value == "text/markdown" || preferred.MediaType.Value == "text/plain")
                {
                    var content = "# www.rosecottagecroft.co.uk\n\n> Rose Cottage Croft is an online store. Use these endpoints to browse products or get information about the shop.\n\n## Endpoints\n\n- `GET /` — Homepage. Contains categories, featured products, and site navigation.\n- `GET /search?q={query}` — Search the store for products matching the query.\n- `GET /contactus` — Contact information and form.\n\n## Authentication\n\nNo authentication is required to browse the store catalog.\n\n## Examples\n\nSearch for a product:\n\n```bash\ncurl https://www.rosecottagecroft.co.uk/search?q=widget\n```";
                    return Content(content, preferred.MediaType.Value);
                }
                else if (preferred.MediaType.Value == "application/json")
                {
                    return Json(new { name = "www.rosecottagecroft.co.uk", description = "Rose Cottage Croft online store" });
                }
            }
        }

        return View();
    }
}