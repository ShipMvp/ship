using Microsoft.Extensions.Logging;
using ShipMvp.Core.Abstractions;
using ShipMvp.Core.Security;
using ShipMvp.Domain.Analytics;
using System.Text.Json;

namespace ShipMvp.Application.Infrastructure.Analytics.Services;

public interface ILlmLoggingService : IScopedService
{
    Task<LlmLog> StartLoggingAsync(
        string pluginName,
        string functionName,
        string modelId,
        string serviceId,
        string renderedPrompt,
        string arguments,
        string? sessionId = null,
        string? requestId = null,
        string? additionalMetadata = null,
        CancellationToken cancellationToken = default);

    Task CompleteLoggingAsync(
        Guid logId,
        string response,
        TimeSpan executionDuration,
        IReadOnlyDictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    Task LogErrorAsync(
        Guid logId,
        Exception exception,
        TimeSpan executionDuration,
        CancellationToken cancellationToken = default);
}

public sealed class LlmLoggingService : ILlmLoggingService
{
    private readonly ILlmLogRepository _llmLogRepository;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<LlmLoggingService> _logger;

    public LlmLoggingService(
        ILlmLogRepository llmLogRepository,
        ICurrentUser currentUser,
        ILogger<LlmLoggingService> logger)
    {
        _llmLogRepository = llmLogRepository;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<LlmLog> StartLoggingAsync(
        string pluginName,
        string functionName,
        string modelId,
        string serviceId,
        string renderedPrompt,
        string arguments,
        string? sessionId = null,
        string? requestId = null,
        string? additionalMetadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var logId = Guid.NewGuid();
            var userId = _currentUser.Id;

            var llmLog = new LlmLog(
                logId,
                pluginName,
                functionName,
                modelId,
                serviceId,
                renderedPrompt,
                arguments,
                userId,
                sessionId,
                requestId);
            llmLog.SetAdditionalMetadata(additionalMetadata);

            await _llmLogRepository.AddAsync(llmLog, cancellationToken);
            
            _logger.LogDebug("Started LLM logging for {Plugin}.{Function} with ID {LogId}", 
                pluginName, functionName, logId);
                
            return llmLog;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start LLM logging for {Plugin}.{Function}", 
                pluginName, functionName);
            throw;
        }
    }

    public async Task CompleteLoggingAsync(
        Guid logId,
        string response,
        TimeSpan executionDuration,
        IReadOnlyDictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var llmLog = await _llmLogRepository.GetByIdAsync(logId, cancellationToken);
            if (llmLog == null)
            {
                _logger.LogWarning("LLM log with ID {LogId} not found for completion", logId);
                return;
            }

            // Extract token usage from metadata
            var tokenUsage = TokenUsage.FromMetadata(metadata);
            var modelInfo = ModelInfo.FromMetadata(metadata, llmLog.ModelId, llmLog.ServiceId);

            llmLog
                .SetTokenUsage(tokenUsage)
                .SetModelInfo(modelInfo)
                .SetExecutionResult(true, response, executionDuration);

            await _llmLogRepository.UpdateAsync(llmLog, cancellationToken);

            _logger.LogDebug("Completed LLM logging for ID {LogId}. Tokens: {TotalTokens}, Duration: {Duration}ms", 
                logId, tokenUsage.TotalTokenCount, executionDuration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete LLM logging for ID {LogId}", logId);
        }
    }

    public async Task LogErrorAsync(
        Guid logId,
        Exception exception,
        TimeSpan executionDuration,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var llmLog = await _llmLogRepository.GetByIdAsync(logId, cancellationToken);
            if (llmLog == null)
            {
                _logger.LogWarning("LLM log with ID {LogId} not found for error logging", logId);
                return;
            }

            llmLog.SetExecutionResult(
                false, 
                string.Empty, 
                executionDuration, 
                exception.Message);

            await _llmLogRepository.UpdateAsync(llmLog, cancellationToken);
            
            _logger.LogDebug("Logged error for LLM call ID {LogId}: {Error}", logId, exception.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log error for LLM call ID {LogId}", logId);
        }
    }

    private static string? SerializeMetadata(IReadOnlyDictionary<string, object>? metadata)
    {
        try
        {
            if (metadata == null || metadata.Count == 0)
                return null;

            return JsonSerializer.Serialize(metadata, new JsonSerializerOptions 
            { 
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
        }
        catch (Exception)
        {
            return null;
        }
    }
}
