using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Web.Factories;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Web.Controllers;

public partial class HomeController : BasePublicController
{
    protected readonly ILlmsModelFactory _llmsModelFactory;
    protected readonly IStoreContext _storeContext;

    public HomeController(ILlmsModelFactory llmsModelFactory, IStoreContext storeContext)
    {
        _llmsModelFactory = llmsModelFactory;
        _storeContext = storeContext;
    }

    [SaveLastContinueShoppingPage]
    public virtual async Task<IActionResult> Index()
    {
        Response.Headers["Vary"] = "Accept";
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
                    var content = await _llmsModelFactory.PrepareLlmsTextFileAsync(false);
                    return Content(content, preferred.MediaType.Value, Encoding.UTF8);
                }
                else if (preferred.MediaType.Value == "application/json")
                {
                    var store = await _storeContext.GetCurrentStoreAsync();
                    return Json(new { name = store.Name, description = $"{store.Name} online store", url = store.Url });
                }
            }
        }

        return View();
    }
}