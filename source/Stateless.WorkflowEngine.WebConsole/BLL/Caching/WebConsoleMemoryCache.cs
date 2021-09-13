using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.BLL.Caching
{

    public class WebConsoleMemoryCache : MemoryCache
    {
        public WebConsoleMemoryCache(IOptions<MemoryCacheOptions> optionsAccessor) : base(optionsAccessor)
        {
        }
    }

    
}
