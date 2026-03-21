using System;
using System.Threading.Tasks;
using Nop.Services.Common;
using Nop.Services.Discounts;
using Nop.Services.Plugins;

namespace Nop.Plugin.Widgets.ImagePuzzle;

public partial class PuzzleRequirementRule : BasePlugin, IDiscountRequirementRule
{
    private readonly IGenericAttributeService _genericAttributeService;

    public PuzzleRequirementRule(IGenericAttributeService genericAttributeService)
    {
        _genericAttributeService = genericAttributeService;
    }

    public async Task<DiscountRequirementValidationResult> CheckRequirementAsync(DiscountRequirementValidationRequest request)
    {
        // Check if the "PuzzleSolved" flag exists in the customer's generic attributes
        var isSolved = await _genericAttributeService.GetAttributeAsync<bool>(request.Customer, "PuzzleSolved");

        return new DiscountRequirementValidationResult
        {
            IsValid = isSolved,
            UserError = "You must solve the product puzzle to use this discount!"
        };
    }

    public string GetConfigurationUrl(int discountId, int? discountRequirementId)
        => "Plugins/ImagePuzzle/ConfigureRequirement";
}
