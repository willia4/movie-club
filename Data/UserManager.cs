using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using zinfandel_movie_club.Data.Models;

namespace zinfandel_movie_club.Data;

public interface IUserManager
{
    public Task<ImmutableList<IGraphUser>> GetUsersWithoutProfilesAsync(CancellationToken cancellationToken);
    public IAsyncEnumerable<IUserProfile> GetUserProfilesAsync(CancellationToken cancellationToken);
    public Task<IUserProfile?> GetProfileForGraphUser(IGraphUser graphUser, CancellationToken cancellationToken);
    public Task<IUserProfile?> GetProfileForUserId(string userId, CancellationToken cancellationToken);
    public Task<IUserProfile> AddOrUpdateProfileForGraphUser(IGraphUser graphUser, string role, CancellationToken cancellationToken);
    public Task<string?> GetRoleForUser(IGraphUser graphUser, CancellationToken cancellationToken);
    public Task<string?> GetRoleForUser(string id, CancellationToken cancellationToken);
}

public class UserManager : IUserManager
{
    private readonly IGraphUserManager _graphUserManager;
    private readonly IUserProfileDatabase _profileDatabase;

    private readonly ConcurrentDictionary<string, RoleCacheEntry> _roleCache = new();

    private record RoleCacheEntry(DateTimeOffset Expiration, string? Value);
    
    public UserManager(IGraphUserManager graphUserManager, IUserProfileDatabase profileDatabase)
    {
        _graphUserManager = graphUserManager;
        _profileDatabase = profileDatabase;
    }

    public async Task<ImmutableList<IGraphUser>> GetUsersWithoutProfilesAsync(CancellationToken cancellationToken)
    {
        var allUserProfiles =
            await _profileDatabase
                .GetUserProfilesAsync(cancellationToken)
                .Select(p => p.Id, cancellationToken: cancellationToken)
                .ToImmutableHashSet(cancellationToken);

        var allGraphUsers = await _graphUserManager.GetGraphUsersAsync(cancellationToken).ToImmutableList(cancellationToken);

        return allGraphUsers
            .Where(g => !allUserProfiles.Contains(g.NameIdentifier))
            .ToImmutableList();
    }

    public async IAsyncEnumerable<IUserProfile> GetUserProfilesAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in _profileDatabase.GetUserProfilesAsync(cancellationToken).OfType<IUserProfile>(cancellationToken))
        {
            var _ = UpdateRoleCacheFromProfile(item);
            yield return item;
        }
    }

    public Task<IUserProfile?> GetProfileForGraphUser(IGraphUser graphUser, CancellationToken cancellationToken)
    {
        return GetProfileForUserId(graphUser.NameIdentifier, cancellationToken);
    }

    public async Task<IUserProfile?> GetProfileForUserId(string userId, CancellationToken cancellationToken)
    {
        var profile = await _profileDatabase.GetUserProfileByIdAsync(userId, cancellationToken);
        var _ = UpdateRoleCacheFromProfile(profile);
        return profile;
    }

    public async Task<IUserProfile> AddOrUpdateProfileForGraphUser(IGraphUser graphUser, string role, CancellationToken cancellationToken)
    {
        var newProfile = await _profileDatabase.UpsertUserProfileForGraphUser(graphUser, role, cancellationToken);
        var _ = UpdateRoleCacheFromProfile(newProfile);
        return newProfile;
    }

    public Task<string?> GetRoleForUser(IGraphUser graphUser, CancellationToken cancellationToken)
    {
        return GetRoleForUser(graphUser.NameIdentifier, cancellationToken);
    }

    public async Task<string?> GetRoleForUser(string id, CancellationToken cancellationToken)
    {
        if (_roleCache.TryGetValue(id, out var cacheEntry))
        {
            if (cacheEntry.Expiration < DateTimeOffset.Now)
            {
                return cacheEntry.Value;
            }
        }

        var profile = await _profileDatabase.GetUserProfileByIdAsync(id, cancellationToken);
        return UpdateRoleCacheFromProfile(profile);
    }

    private string? UpdateRoleCacheFromProfile(IUserProfile? profile)
    {
        if (profile == null) return null;
        
        var newCacheEntry = new RoleCacheEntry(DateTimeOffset.Now.AddMinutes(10), profile.Role);
        return (_roleCache.AddOrUpdate(profile.Id, _ => newCacheEntry, (_, _) => newCacheEntry)).Value;
    }
}
