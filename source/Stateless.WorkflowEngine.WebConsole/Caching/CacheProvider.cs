using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stateless.WorkflowEngine.WebConsole.Caching
{
    public interface ICacheProvider
    {
        bool ContainsKey(string cacheKey);

        T Get<T>(string cacheKey);

        void Set<T>(string cacheKey, T value, TimeSpan absoluteExpirationRelativeToNow);
    }

    public class CacheProvider : ICacheProvider
    {
        private readonly IMemoryCache _memoryCache;

        public CacheProvider(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        public bool ContainsKey(string cacheKey)
        {
            return (_memoryCache.Get(cacheKey) != null);
        }

        public T Get<T>(string cacheKey)
        {
            return _memoryCache.Get<T>(cacheKey);
        }

        public void Set<T>(string cacheKey, T value, TimeSpan absoluteExpirationRelativeToNow)
        {
            _memoryCache.Set<T>(cacheKey, value, absoluteExpirationRelativeToNow);
        }


    }
}
