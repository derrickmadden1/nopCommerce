using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Nop.Web.Framework.Models;

namespace Nop.Plugin.Widgets.ImagePuzzle.Models;

public partial record PuzzleModel : BaseNopModel
{
    public int ProductId { get; set; }
    public int GridSize { get; set; }
}
