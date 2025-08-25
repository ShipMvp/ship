using ShipMvp.Core.Services;
using ShipMvp.Core.Abstractions;
using ShipMvp.Domain.Analytics;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ShipMvp.Application.Analytics;

public sealed record LlmUsageStatsDto(
    int TotalCalls,
    int SuccessfulCalls,
    int FailedCalls,
    int TotalTokens,
    decimal EstimatedCost,
    Dictionary<string, int> UsageByPlugin,
    Dictionary<string, int> UsageByModel,
    TimeSpan AverageExecutionTime);

public sealed record LlmLogDto(
    Guid Id,
    string PluginName,
    string FunctionName,
    string ModelId,
    string ServiceId,
    string? FinishReason,
    int PromptTokenCount,
    int CandidatesTokenCount,
    int TotalTokenCount,
    TimeSpan ExecutionDuration,
    bool IsSuccess,
    string? ErrorMessage,
    DateTime CreatedAt,
    Guid? UserId,
    string? SessionId);

public sealed record LlmLogDetailDto(
    Guid Id,
    string PluginName,
    string FunctionName,
    string ModelId,
    string ServiceId,
    string? FinishReason,
    int PromptTokenCount,
    int CandidatesTokenCount,
    int TotalTokenCount,
    string RenderedPrompt,
    string Response,
    string Arguments,
    TimeSpan ExecutionDuration,
    bool IsSuccess,
    string? ErrorMessage,
    DateTime CreatedAt,
    Guid? UserId,
    string? SessionId,
    string? AdditionalMetadata);

public interface ILlmAnalyticsService : ITransientService
{
    Task<LlmUsageStatsDto> GetUsageStatsAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<LlmLogDto>> GetRecentCallsAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<LlmLogDto>> GetFailedCallsAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task<LlmLogDetailDto?> GetCallDetailAsync(Guid logId, CancellationToken cancellationToken = default);
    Task<IEnumerable<LlmLogDto>> GetCallsByPluginAsync(string pluginName, int limit = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<LlmLogDto>> GetCallsByUserAsync(Guid userId, int limit = 100, CancellationToken cancellationToken = default);
    Task<IEnumerable<LlmLogDto>> GetCallsBySessionAsync(string sessionId, CancellationToken cancellationToken = default);
}

public sealed class LlmAnalyticsService : DomainService, ILlmAnalyticsService
{
    private readonly ILlmLogRepository _llmLogRepository;

    public LlmAnalyticsService(
        ILlmLogRepository llmLogRepository,
        IGuidGenerator guidGenerator,
        ILoggerFactory loggerFactory) : base(guidGenerator, loggerFactory)
    {
        _llmLogRepository = llmLogRepository;
    }

    public async Task<LlmUsageStatsDto> GetUsageStatsAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
    {
        var logs = await _llmLogRepository.GetByDateRangeAsync(
            startDate ?? DateTime.UtcNow.AddDays(-30), 
            endDate ?? DateTime.UtcNow, 
            cancellationToken);

        var logList = logs.ToList();
        var totalCalls = logList.Count;
        var successfulCalls = logList.Count(x => x.IsSuccess);
        var failedCalls = totalCalls - successfulCalls;
        var totalTokens = await _llmLogRepository.GetTotalTokenUsageAsync(startDate, endDate, cancellationToken);
        var estimatedCost = await _llmLogRepository.GetEstimatedCostAsync(startDate, endDate, cancellationToken);
        var usageByPlugin = await _llmLogRepository.GetUsageByPluginAsync(startDate, endDate, cancellationToken);
        var usageByModel = await _llmLogRepository.GetUsageByModelAsync(startDate, endDate, cancellationToken);
        
        var averageExecutionTime = logList.Any() 
            ? TimeSpan.FromMilliseconds(logList.Average(x => x.ExecutionDuration.TotalMilliseconds))
            : TimeSpan.Zero;

        return new LlmUsageStatsDto(
            totalCalls,
            successfulCalls,
            failedCalls,
            totalTokens,
            estimatedCost,
            usageByPlugin,
            usageByModel,
            averageExecutionTime);
    }

    public async Task<IEnumerable<LlmLogDto>> GetRecentCallsAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        var logs = await _llmLogRepository.GetByDateRangeAsync(
            DateTime.UtcNow.AddDays(-7), 
            DateTime.UtcNow, 
            cancellationToken);

        return logs.OrderByDescending(x => x.CreatedAt)
                   .Take(limit)
                   .Select(MapToDto);
    }

    public async Task<IEnumerable<LlmLogDto>> GetFailedCallsAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        var logs = await _llmLogRepository.GetFailedCallsAsync(cancellationToken);

        return logs.Take(limit)
                   .Select(MapToDto);
    }

    public async Task<LlmLogDetailDto?> GetCallDetailAsync(Guid logId, CancellationToken cancellationToken = default)
    {
        var log = await _llmLogRepository.GetByIdAsync(logId, cancellationToken);
        return log != null ? MapToDetailDto(log) : null;
    }

    public async Task<IEnumerable<LlmLogDto>> GetCallsByPluginAsync(string pluginName, int limit = 100, CancellationToken cancellationToken = default)
    {
        var logs = await _llmLogRepository.GetByPluginAsync(pluginName, cancellationToken);

        return logs.Take(limit)
                   .Select(MapToDto);
    }

    public async Task<IEnumerable<LlmLogDto>> GetCallsByUserAsync(Guid userId, int limit = 100, CancellationToken cancellationToken = default)
    {
        var logs = await _llmLogRepository.GetByUserAsync(userId, cancellationToken);

        return logs.Take(limit)
                   .Select(MapToDto);
    }

    public async Task<IEnumerable<LlmLogDto>> GetCallsBySessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var logs = await _llmLogRepository.GetBySessionAsync(sessionId, cancellationToken);

        return logs.Select(MapToDto);
    }

    private static LlmLogDto MapToDto(LlmLog log)
    {
        return new LlmLogDto(
            log.Id,
            log.PluginName,
            log.FunctionName,
            log.ModelId,
            log.ServiceId,
            log.FinishReason,
            log.PromptTokenCount,
            log.CandidatesTokenCount,
            log.TotalTokenCount,
            log.ExecutionDuration,
            log.IsSuccess,
            log.ErrorMessage,
            log.CreatedAt,
            log.UserId,
            log.SessionId);
    }

    private static LlmLogDetailDto MapToDetailDto(LlmLog log)
    {
        return new LlmLogDetailDto(
            log.Id,
            log.PluginName,
            log.FunctionName,
            log.ModelId,
            log.ServiceId,
            log.FinishReason,
            log.PromptTokenCount,
            log.CandidatesTokenCount,
            log.TotalTokenCount,
            log.RenderedPrompt,
            log.Response,
            log.Arguments,
            log.ExecutionDuration,
            log.IsSuccess,
            log.ErrorMessage,
            log.CreatedAt,
            log.UserId,
            log.SessionId,
            log.AdditionalMetadata);
    }
}
