using System.Linq.Expressions;
using Common.Repositories;
using Data.Context;
using Microsoft.EntityFrameworkCore;
using Models;

namespace Repositories;

public class UserSettingsRepository(AlexandriaDbContext context) : IUserSettingsRepository
{
    private readonly DbSet<UserSettings> _settings = context.UserSettings;

    public async Task<UserSettings?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _settings.FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt == null, ct);
    }

    public async Task<UserSettings?> FirstOrDefaultAsync(Expression<Func<UserSettings, bool>> predicate, CancellationToken ct = default)
    {
        return await _settings.FirstOrDefaultAsync(predicate, ct);
    }

    public async Task<IEnumerable<UserSettings>> GetAllAsync(CancellationToken ct = default)
    {
        return await _settings.Where(s => s.DeletedAt == null).ToListAsync(ct);
    }

    public async Task<IEnumerable<UserSettings>> FindAsync(Expression<Func<UserSettings, bool>> predicate, CancellationToken ct = default)
    {
        return await _settings.Where(s => s.DeletedAt == null).Where(predicate).ToListAsync(ct);
    }

    public async Task<UserSettings> AddAsync(UserSettings entity, CancellationToken ct = default)
    {
        entity.CreatedAt = DateTime.UtcNow;
        var entry = await _settings.AddAsync(entity, ct);
        return entry.Entity;
    }

    public async Task<IEnumerable<UserSettings>> AddRangeAsync(IEnumerable<UserSettings> entities, CancellationToken ct = default)
    {
        var settingsList = entities.ToList();
        var now = DateTime.UtcNow;

        foreach (var entity in settingsList)
        {
            entity.CreatedAt = now;
        }

        await _settings.AddRangeAsync(settingsList, ct);
        return settingsList;
    }

    public void Update(UserSettings entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _settings.Update(entity);
    }

    public void Remove(UserSettings entity)
    {
        _settings.Remove(entity);
    }

    public void RemoveRange(IEnumerable<UserSettings> entities)
    {
        _settings.RemoveRange(entities);
    }

    public async Task<int> CountAsync(Expression<Func<UserSettings, bool>>? predicate = null, CancellationToken ct = default)
    {
        var query = _settings.Where(s => s.DeletedAt == null);
        return predicate == null
            ? await query.CountAsync(ct)
            : await query.CountAsync(predicate, ct);
    }

    public async Task<bool> ExistsAsync(Expression<Func<UserSettings, bool>> predicate, CancellationToken ct = default)
    {
        return await _settings.AnyAsync(predicate, ct);
    }

    public async Task<UserSettings?> GetByKeyAsync(string key, Guid userId, CancellationToken ct = default)
    {
        return await _settings.FirstOrDefaultAsync(s => s.Key == key && s.UserId == userId && s.DeletedAt == null, ct);
    }

    public async Task<IEnumerable<UserSettings>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _settings.Where(s => s.UserId == userId && s.DeletedAt == null).ToListAsync(ct);
    }
}
