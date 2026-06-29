using System.Threading.Tasks;
using Nop.Plugin.Misc.UniversalCommerce.Models;

namespace Nop.Plugin.Misc.UniversalCommerce.Services
{
    public interface IAp2SecurityService
    {
        Task<string> GetGoogleKeysJsonAsync();
        Task<bool> ValidateAgentMandateAsync(string rawTokenJson, decimal systemPrice, string expectedSku);
    }
}
