using System.Linq.Expressions;
using Alexandria.Common.Repositories;
using Alexandria.Data.Context;
using Alexandria.Data.Models;
using Alexandria.Data.Models.Enumerators;
using Microsoft.EntityFrameworkCore;

namespace Alexandria.Repositories;

public class HomeWidgetRepository(AlexandriaDbContext context) : IHomeWidgetRepository
{
    public Task<List<HomeWidget>> GetByUserAsync(Guid userId, HomeLayout? layout = null,
        CancellationToken ct = default)
    {
        var widgets = ActiveWidgets().Where(widget => widget.UserId == userId);

        if (layout.HasValue)
            widgets = widgets.Where(widget => widget.Layout == layout.Value);

        return widgets.OrderBy(widget => widget.Layout)
            .ThenBy(widget => widget.Y)
            .ThenBy(widget => widget.X)
            .ThenBy(widget => widget.Id)
            .ToListAsync(ct);
    }

    public Task<HomeWidget?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
        => ActiveWidgets().SingleOrDefaultAsync(widget => widget.Id == id && widget.UserId == userId, ct);

    public Task<HomeWidget?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => ActiveWidgets().SingleOrDefaultAsync(widget => widget.Id == id, ct);

    public Task<HomeWidget?> FirstOrDefaultAsync(Expression<Func<HomeWidget, bool>> predicate,
        CancellationToken ct = default)
        => ActiveWidgets().FirstOrDefaultAsync(predicate, ct);

    public async Task<IEnumerable<HomeWidget>> FindAsync(Expression<Func<HomeWidget, bool>> predicate,
        CancellationToken ct = default)
        => await ActiveWidgets().Where(predicate).ToListAsync(ct);

    public Task<bool> AnyIdsExistAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
        => context.HomeWidgets.AnyAsync(widget => ids.Contains(widget.Id), ct);

    public async Task<HomeWidget> AddAsync(HomeWidget entity, CancellationToken ct = default)
    {
        entity.CreatedAt = DateTime.UtcNow;

        var entry = await context.HomeWidgets.AddAsync(entity, ct);

        return entry.Entity;
    }

    public async Task<IEnumerable<HomeWidget>> AddRangeAsync(IEnumerable<HomeWidget> entities,
        CancellationToken ct = default)
    {
        var widgets = entities.ToList();
        var now = DateTime.UtcNow;

        foreach (var widget in widgets)
            widget.CreatedAt = now;

        await context.HomeWidgets.AddRangeAsync(widgets, ct);

        return widgets;
    }

    public void Update(HomeWidget entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        context.HomeWidgets.Update(entity);
    }

    public void Remove(HomeWidget entity)
        => context.HomeWidgets.Remove(entity);

    public void RemoveRange(IEnumerable<HomeWidget> widgets)
        => context.HomeWidgets.RemoveRange(widgets);

    public Task<int> CountAsync(Expression<Func<HomeWidget, bool>>? predicate = null,
        CancellationToken ct = default)
    {
        var widgets = ActiveWidgets();

        if (predicate is not null)
            widgets = widgets.Where(predicate);

        return widgets.CountAsync(ct);
    }

    public Task<bool> ExistsAsync(Expression<Func<HomeWidget, bool>> predicate, CancellationToken ct = default)
        => ActiveWidgets().AnyAsync(predicate, ct);

    private IQueryable<HomeWidget> ActiveWidgets()
        => context.HomeWidgets.Where(widget => widget.DeletedAt == null);
}
