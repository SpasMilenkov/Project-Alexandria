using System.Linq.Expressions;
using Common.Repositories;
using Data.Context;
using Microsoft.EntityFrameworkCore;
using Models;

namespace Repositories;

public class AdminSettingsRepository(AlexandriaDbContext context) : IAdminSettingsRepository
{
    private readonly DbSet<AdminSettings> _adminSettings = context.AdminSettings;

    public async Task<AdminSettings?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _adminSettings.FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt == null, ct);
    }

    public async Task<AdminSettings?> FirstOrDefaultAsync(Expression<Func<AdminSettings, bool>> predicate, CancellationToken ct = default)
    {
        return await _adminSettings.FirstOrDefaultAsync(predicate, ct);
    }

    public async Task<IEnumerable<AdminSettings>> GetAllAsync(CancellationToken ct = default)
    {
        return await _adminSettings.Where(s => s.DeletedAt == null).ToListAsync(ct);
    }

    public async Task<IEnumerable<AdminSettings>> FindAsync(Expression<Func<AdminSettings, bool>> predicate, CancellationToken ct = default)
    {
        return await _adminSettings.Where(s => s.DeletedAt == null).Where(predicate).ToListAsync(ct);
    }

    public async Task<AdminSettings> AddAsync(AdminSettings entity, CancellationToken ct = default)
    {
        entity.CreatedAt = DateTime.UtcNow;
        var entry = await _adminSettings.AddAsync(entity, ct);
        return entry.Entity;
    }

    public async Task<IEnumerable<AdminSettings>> AddRangeAsync(IEnumerable<AdminSettings> entities, CancellationToken ct = default)
    {
        var settingsList = entities.ToList();
        var now = DateTime.UtcNow;

        foreach (var entity in settingsList)
        {
            entity.CreatedAt = now;
        }

        await _adminSettings.AddRangeAsync(settingsList, ct);
        return settingsList;
    }

    public void Update(AdminSettings entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        _adminSettings.Update(entity);
    }

    public void Remove(AdminSettings entity)
    {
        _adminSettings.Remove(entity);
    }

    public void RemoveRange(IEnumerable<AdminSettings> entities)
    {
        _adminSettings.RemoveRange(entities);
    }

    public async Task<int> CountAsync(Expression<Func<AdminSettings, bool>>? predicate = null, CancellationToken ct = default)
    {
        var query = _adminSettings.Where(s => s.DeletedAt == null);
        return predicate == null
            ? await query.CountAsync(ct)
            : await query.CountAsync(predicate, ct);
    }

    public async Task<bool> ExistsAsync(Expression<Func<AdminSettings, bool>> predicate, CancellationToken ct = default)
    {
        return await _adminSettings.AnyAsync(predicate, ct);
    }

    public async Task<AdminSettings?> GetByKeyAsync(string key, CancellationToken ct = default)
    {
        return await _adminSettings.FirstOrDefaultAsync(s => s.Key == key && s.DeletedAt == null, ct);
    }
}
