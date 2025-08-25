using Microsoft.EntityFrameworkCore;
using ShipMvp.Core.Abstractions;
using ShipMvp.Core.Persistence;
using ShipMvp.Core.Attributes;
using ShipMvp.Domain.Analytics;

namespace ShipMvp.Application.Infrastructure.Analytics.Data;

[UnitOfWork]
public sealed class LlmLogRepository : ILlmLogRepository
{
    private readonly IDbContext _context;
    private readonly DbSet<LlmLog> _dbSet;

    public LlmLogRepository(IDbContext context)
    {
        _context = context;
        _dbSet = context.Set<LlmLog>();
    }

    // IRepository<LlmLog, Guid> implementation
    public async Task<LlmLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
    }

    public async Task<IEnumerable<LlmLog>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(x => !x.IsDeleted).ToListAsync(cancellationToken);
    }

    public async Task<LlmLog> AddAsync(LlmLog entity, CancellationToken cancellationToken = default)
    {
        var entry = await _dbSet.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return entry.Entity;
    }

    public async Task<LlmLog> UpdateAsync(LlmLog entity, CancellationToken cancellationToken = default)
    {
        var entry = _dbSet.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return entry.Entity;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity != null)
        {
            // Soft delete by setting IsDeleted flag
            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            await UpdateAsync(entity, cancellationToken);
        }
    }

    // ILlmLogRepository implementation
    public async Task<IEnumerable<LlmLog>> GetByPluginAsync(string pluginName, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(x => x.PluginName == pluginName && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<LlmLog>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(x => x.UserId == userId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<LlmLog>> GetBySessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(x => x.SessionId == sessionId && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<LlmLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(x => x.CreatedAt >= startDate && x.CreatedAt <= endDate && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<LlmLog>> GetFailedCallsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(x => !x.IsSuccess && !x.IsDeleted)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetTotalTokenUsageAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(x => !x.IsDeleted);
        
        if (startDate.HasValue)
            query = query.Where(x => x.CreatedAt >= startDate.Value);
            
        if (endDate.HasValue)
            query = query.Where(x => x.CreatedAt <= endDate.Value);

        return await query.SumAsync(x => x.TotalTokenCount, cancellationToken);
    }

    public async Task<decimal> GetEstimatedCostAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(x => !x.IsDeleted);
        
        if (startDate.HasValue)
            query = query.Where(x => x.CreatedAt >= startDate.Value);
            
        if (endDate.HasValue)
            query = query.Where(x => x.CreatedAt <= endDate.Value);

        var logs = await query.ToListAsync(cancellationToken);
        
        return logs.Sum(log => log.CalculateEstimatedCost());
    }

    public async Task<Dictionary<string, int>> GetUsageByPluginAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(x => !x.IsDeleted);
        
        if (startDate.HasValue)
            query = query.Where(x => x.CreatedAt >= startDate.Value);
            
        if (endDate.HasValue)
            query = query.Where(x => x.CreatedAt <= endDate.Value);

        return await query
            .GroupBy(x => x.PluginName)
            .Select(g => new { Plugin = g.Key, TotalTokens = g.Sum(x => x.TotalTokenCount) })
            .ToDictionaryAsync(x => x.Plugin, x => x.TotalTokens, cancellationToken);
    }

    public async Task<Dictionary<string, int>> GetUsageByModelAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(x => !x.IsDeleted);
        
        if (startDate.HasValue)
            query = query.Where(x => x.CreatedAt >= startDate.Value);
            
        if (endDate.HasValue)
            query = query.Where(x => x.CreatedAt <= endDate.Value);

        return await query
            .GroupBy(x => x.ModelId)
            .Select(g => new { Model = g.Key, TotalTokens = g.Sum(x => x.TotalTokenCount) })
            .ToDictionaryAsync(x => x.Model, x => x.TotalTokens, cancellationToken);
    }
}
