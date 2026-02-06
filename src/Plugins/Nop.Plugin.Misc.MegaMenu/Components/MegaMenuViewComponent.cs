using Microsoft.AspNetCore.Mvc;
using Nop.Services.Catalog;
using Nop.Services.Seo;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Nop.Plugin.Misc.MegaMenu.Models;

namespace Nop.Plugin.Misc.MegaMenu.Components
{
    public class MegaMenuViewComponent : ViewComponent
    {
        private readonly ICategoryService _categoryService;
        private readonly IUrlRecordService _urlRecordService;

        public MegaMenuViewComponent(
            ICategoryService categoryService,
            IUrlRecordService urlRecordService)
        {
            _categoryService = categoryService;
            _urlRecordService = urlRecordService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // Load all categories once
            var all = await _categoryService.GetAllCategoriesAsync(showHidden: false);

            // Build root categories
            var roots = new List<MegaMenuItemModel>();

            foreach (var root in all.Where(c => c.ParentCategoryId == 0).OrderBy(c => c.DisplayOrder))
            {
                var rootModel = new MegaMenuItemModel
                {
                    Name = root.Name,
                    SeName = await _urlRecordService.GetSeNameAsync(root)
                };

                // Load children
                var children = all
                    .Where(x => x.ParentCategoryId == root.Id)
                    .OrderBy(x => x.DisplayOrder)
                    .ToList();

                foreach (var child in children)
                {
                    var childModel = new MegaMenuItemModel
                    {
                        Name = child.Name,
                        SeName = await _urlRecordService.GetSeNameAsync(child)
                    };

                    // Load grandchildren
                    var grandchildren = all
                        .Where(x => x.ParentCategoryId == child.Id)
                        .OrderBy(x => x.DisplayOrder)
                        .ToList();

                    foreach (var grand in grandchildren)
                    {
                        childModel.Children.Add(new MegaMenuItemModel
                        {
                            Name = grand.Name,
                            SeName = await _urlRecordService.GetSeNameAsync(grand)
                        });
                    }

                    rootModel.Children.Add(childModel);
                }

                roots.Add(rootModel);
            }

            return View(
                "~/Plugins/Misc.MegaMenu/Views/Shared/Components/MegaMenu/Default.cshtml",
                roots
            );
        }
    }
}