using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OsuRTDataProvider.Listen.OsuListenerManager;

namespace OBSControl
{
    public class ControlItem
    {
        public List<string> Commands { get; set; } = new List<string>();

        [JsonIgnore]
        public Action CachedExecutableAction = ()=> { };
    }
}
