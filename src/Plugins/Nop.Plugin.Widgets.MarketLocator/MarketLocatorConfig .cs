using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nop.Core.Configuration;

namespace Nop.Plugin.Widgets.MarketLocator;

public class MarketLocatorConfig : IConfig
{
    public string ServiceBusConnectionString { get; set; } = string.Empty;
    public string QueueName {  get; set; } = string.Empty;
}
