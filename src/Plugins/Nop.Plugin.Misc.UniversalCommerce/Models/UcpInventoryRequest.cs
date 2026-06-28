namespace Nop.Plugin.Misc.UniversalCommerce.Models
{
    public class UcpInventoryRequest
    {
        public string Sku { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }
}
