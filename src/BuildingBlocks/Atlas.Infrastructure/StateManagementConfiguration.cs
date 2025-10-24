using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AtlasBank.Infrastructure.StateManagement;

/// <summary>
/// Application state management service
/// </summary>
public interface IStateManager
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);
    Task RemoveAsync(string key);
    Task<bool> ExistsAsync(string key);
    Task ClearAsync();
    Task<Dictionary<string, object>> GetAllAsync();
}

public class StateManager : IStateManager
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<StateManager> _logger;
    private readonly Dictionary<string, object> _persistentState = new();

    public StateManager(IMemoryCache cache, ILogger<StateManager> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            // Try memory cache first
            if (_cache.TryGetValue(key, out var cachedValue))
            {
                return (T?)cachedValue;
            }

            // Try persistent state
            if (_persistentState.TryGetValue(key, out var persistentValue))
            {
                if (persistentValue is T directValue)
                {
                    return directValue;
                }

                if (persistentValue is string jsonValue)
                {
                    return JsonSerializer.Deserialize<T>(jsonValue);
                }
            }

            return default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting state for key: {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        try
        {
            // Set in memory cache
            var cacheOptions = new MemoryCacheEntryOptions();
            if (expiration.HasValue)
            {
                cacheOptions.AbsoluteExpirationRelativeToNow = expiration;
            }
            else
            {
                cacheOptions.SlidingExpiration = TimeSpan.FromMinutes(30);
            }

            _cache.Set(key, value, cacheOptions);

            // Set in persistent state
            _persistentState[key] = value;

            _logger.LogDebug("State set for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting state for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            _cache.Remove(key);
            _persistentState.Remove(key);
            _logger.LogDebug("State removed for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing state for key: {Key}", key);
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        return _cache.TryGetValue(key, out _) || _persistentState.ContainsKey(key);
    }

    public async Task ClearAsync()
    {
        try
        {
            _cache.Dispose();
            _persistentState.Clear();
            _logger.LogInformation("All state cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing state");
        }
    }

    public async Task<Dictionary<string, object>> GetAllAsync()
    {
        var allState = new Dictionary<string, object>();

        // Add persistent state
        foreach (var kvp in _persistentState)
        {
            allState[kvp.Key] = kvp.Value;
        }

        return allState;
    }
}

/// <summary>
/// User session management
/// </summary>
public interface IUserSessionManager
{
    Task<UserSession?> GetSessionAsync(string sessionId);
    Task<UserSession> CreateSessionAsync(string userId, Dictionary<string, object>? metadata = null);
    Task UpdateSessionAsync(string sessionId, Dictionary<string, object> updates);
    Task EndSessionAsync(string sessionId);
    Task<bool> IsSessionValidAsync(string sessionId);
    Task<List<UserSession>> GetActiveSessionsAsync(string userId);
}

public class UserSessionManager : IUserSessionManager
{
    private readonly IStateManager _stateManager;
    private readonly ILogger<UserSessionManager> _logger;

    public UserSessionManager(IStateManager stateManager, ILogger<UserSessionManager> logger)
    {
        _stateManager = stateManager;
        _logger = logger;
    }

    public async Task<UserSession?> GetSessionAsync(string sessionId)
    {
        var key = $"session:{sessionId}";
        return await _stateManager.GetAsync<UserSession>(key);
    }

    public async Task<UserSession> CreateSessionAsync(string userId, Dictionary<string, object>? metadata = null)
    {
        var sessionId = Guid.NewGuid().ToString();
        var session = new UserSession
        {
            SessionId = sessionId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow,
            IsActive = true,
            Metadata = metadata ?? new Dictionary<string, object>()
        };

        var key = $"session:{sessionId}";
        await _stateManager.SetAsync(key, session, TimeSpan.FromHours(24));

        // Track user sessions
        var userSessionsKey = $"user_sessions:{userId}";
        var userSessions = await _stateManager.GetAsync<List<string>>(userSessionsKey) ?? new List<string>();
        userSessions.Add(sessionId);
        await _stateManager.SetAsync(userSessionsKey, userSessions, TimeSpan.FromHours(24));

        _logger.LogInformation("Session created for user: {UserId}, SessionId: {SessionId}", userId, sessionId);
        return session;
    }

    public async Task UpdateSessionAsync(string sessionId, Dictionary<string, object> updates)
    {
        var session = await GetSessionAsync(sessionId);
        if (session == null)
        {
            _logger.LogWarning("Session not found: {SessionId}", sessionId);
            return;
        }

        foreach (var update in updates)
        {
            session.Metadata[update.Key] = update.Value;
        }

        session.LastAccessedAt = DateTime.UtcNow;

        var key = $"session:{sessionId}";
        await _stateManager.SetAsync(key, session, TimeSpan.FromHours(24));
    }

    public async Task EndSessionAsync(string sessionId)
    {
        var session = await GetSessionAsync(sessionId);
        if (session == null)
        {
            _logger.LogWarning("Session not found: {SessionId}", sessionId);
            return;
        }

        session.IsActive = false;
        session.EndedAt = DateTime.UtcNow;

        var key = $"session:{sessionId}";
        await _stateManager.SetAsync(key, session, TimeSpan.FromHours(1)); // Keep for 1 hour for audit

        _logger.LogInformation("Session ended: {SessionId}", sessionId);
    }

    public async Task<bool> IsSessionValidAsync(string sessionId)
    {
        var session = await GetSessionAsync(sessionId);
        if (session == null)
            return false;

        if (!session.IsActive)
            return false;

        // Check if session is expired (24 hours)
        if (DateTime.UtcNow - session.LastAccessedAt > TimeSpan.FromHours(24))
        {
            await EndSessionAsync(sessionId);
            return false;
        }

        return true;
    }

    public async Task<List<UserSession>> GetActiveSessionsAsync(string userId)
    {
        var userSessionsKey = $"user_sessions:{userId}";
        var sessionIds = await _stateManager.GetAsync<List<string>>(userSessionsKey) ?? new List<string>();

        var activeSessions = new List<UserSession>();
        foreach (var sessionId in sessionIds)
        {
            var session = await GetSessionAsync(sessionId);
            if (session != null && session.IsActive)
            {
                activeSessions.Add(session);
            }
        }

        return activeSessions;
    }
}

/// <summary>
/// User session model
/// </summary>
public class UserSession
{
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastAccessedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsActive { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Application state model
/// </summary>
public class ApplicationState
{
    public Dictionary<string, object> GlobalState { get; set; } = new();
    public Dictionary<string, UserState> UserStates { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

public class UserState
{
    public string UserId { get; set; } = string.Empty;
    public Dictionary<string, object> State { get; set; } = new();
    public DateTime LastAccessed { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// State management configuration
/// </summary>
public static class StateManagementConfiguration
{
    public static void ConfigureStateManagement(this IServiceCollection services)
    {
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 1000;
            options.CompactionPercentage = 0.25;
            options.ExpirationScanFrequency = TimeSpan.FromMinutes(5);
        });

        services.AddSingleton<IStateManager, StateManager>();
        services.AddScoped<IUserSessionManager, UserSessionManager>();
    }
}
