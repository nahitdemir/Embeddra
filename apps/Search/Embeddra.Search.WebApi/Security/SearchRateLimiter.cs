using System.Collections.Concurrent;

namespace Embeddra.Search.WebApi.Security;

public sealed class SearchRateLimiter
{
    private readonly SearchRateLimitOptions _options;
    private readonly ConcurrentDictionary<string, RateLimitState> _states = new(StringComparer.Ordinal);

    public SearchRateLimiter(SearchRateLimitOptions options)
    {
        _options = options;
    }

    public bool TryConsume(string key)
    {
        if (!_options.Enabled)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            return true;
        }

        var state = _states.GetOrAdd(key, _ => new RateLimitState(DateTimeOffset.UtcNow));
        lock (state.Lock)
        {
            var now = DateTimeOffset.UtcNow;
            if (now - state.WindowStart >= _options.Window)
            {
                state.WindowStart = now;
                state.Count = 0;
            }

            if (state.Count >= _options.RequestsPerMinute)
            {
                return false;
            }

            state.Count++;
            return true;
        }
    }

    private sealed class RateLimitState
    {
        public RateLimitState(DateTimeOffset windowStart)
        {
            WindowStart = windowStart;
        }

        public DateTimeOffset WindowStart { get; set; }
        public int Count { get; set; }
        public object Lock { get; } = new();
    }
}
