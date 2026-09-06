using System;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Marketing.WinbackEmail.Models;

public record UpcomingEmailModel : BaseNopModel
{
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public int EmailSequenceNumber { get; set; }
    public DateTime ScheduledDateUtc { get; set; }
}

