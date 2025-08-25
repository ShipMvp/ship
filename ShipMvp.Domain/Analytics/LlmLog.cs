using ShipMvp.Core.Entities;
using ShipMvp.Core.Abstractions;

namespace ShipMvp.Domain.Analytics;

// Value Objects for structured logging
public sealed record TokenUsage(
    int PromptTokenCount,
    int CandidatesTokenCount,
    int CurrentCandidateTokenCount,
    int TotalTokenCount)
{
    // Parameterless constructor for EF Core
    private TokenUsage() : this(0, 0, 0, 0) { }
    
    public static TokenUsage FromMetadata(IReadOnlyDictionary<string, object>? metadata)
    {
        if (metadata == null) return new TokenUsage();
        
        var promptTokens = GetIntFromMetadata(metadata, "PromptTokenCount");
        var candidatesTokens = GetIntFromMetadata(metadata, "CandidatesTokenCount");
        var currentCandidateTokens = GetIntFromMetadata(metadata, "CurrentCandidateTokenCount");
        var totalTokens = GetIntFromMetadata(metadata, "TotalTokenCount");
        
        return new TokenUsage(promptTokens, candidatesTokens, currentCandidateTokens, totalTokens);
    }
    
    private static int GetIntFromMetadata(IReadOnlyDictionary<string, object> metadata, string key)
    {
        return metadata.TryGetValue(key, out var value) && value is int intValue ? intValue : 0;
    }
}

public sealed record ModelInfo(
    string ModelId,
    string ServiceId,
    string? FinishReason = null)
{
    // Parameterless constructor for EF Core
    private ModelInfo() : this(string.Empty, string.Empty) { }
    
    public static ModelInfo FromMetadata(IReadOnlyDictionary<string, object>? metadata, string modelId, string serviceId)
    {
        var finishReason = metadata?.TryGetValue("FinishReason", out var reason) == true 
            ? reason?.ToString() 
            : null;
            
        return new ModelInfo(modelId, serviceId, finishReason);
    }
}

// Domain Entity
public sealed class LlmLog : AggregateRoot<Guid>
{
    public string PluginName { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string ServiceId { get; set; } = string.Empty;
    public string? FinishReason { get; set; }
    
    // Token usage information
    public int PromptTokenCount { get; set; }
    public int CandidatesTokenCount { get; set; }
    public int CurrentCandidateTokenCount { get; set; }
    public int TotalTokenCount { get; set; }
    
    // Request/Response data
    public string RenderedPrompt { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty; // JSON serialized arguments
    
    // Execution metrics
    public TimeSpan ExecutionDuration { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    
    // Audit fields
    public Guid? UserId { get; set; }
    public string? SessionId { get; set; }
    public string? RequestId { get; set; }
    
    // Additional metadata as JSON
    public string? AdditionalMetadata { get; set; }

    // Parameterless constructor for EF Core
    private LlmLog() : base(Guid.Empty) { }

    public LlmLog(
        Guid id,
        string pluginName,
        string functionName,
        string modelId,
        string serviceId,
        string renderedPrompt,
        string arguments,
        Guid? userId = null,
        string? sessionId = null,
        string? requestId = null) : base(id)
    {
        PluginName = pluginName ?? throw new ArgumentNullException(nameof(pluginName));
        FunctionName = functionName ?? throw new ArgumentNullException(nameof(functionName));
        ModelId = modelId ?? throw new ArgumentNullException(nameof(modelId));
        ServiceId = serviceId ?? throw new ArgumentNullException(nameof(serviceId));
        RenderedPrompt = renderedPrompt ?? string.Empty;
        Arguments = arguments ?? string.Empty;
        IsSuccess = false; // Will be updated when execution completes
        UserId = userId;
        SessionId = sessionId;
        RequestId = requestId;
    }
    
    public LlmLog SetTokenUsage(TokenUsage tokenUsage)
    {
        PromptTokenCount = tokenUsage.PromptTokenCount;
        CandidatesTokenCount = tokenUsage.CandidatesTokenCount;
        CurrentCandidateTokenCount = tokenUsage.CurrentCandidateTokenCount;
        TotalTokenCount = tokenUsage.TotalTokenCount;
        return this;
    }
    
    public LlmLog SetModelInfo(ModelInfo modelInfo)
    {
        FinishReason = modelInfo.FinishReason;
        return this;
    }
    
    public LlmLog SetExecutionResult(
        bool isSuccess,
        string response,
        TimeSpan executionDuration,
        string? errorMessage = null)
    {
        IsSuccess = isSuccess;
        Response = response ?? string.Empty;
        ExecutionDuration = executionDuration;
        ErrorMessage = errorMessage;
        return this;
    }
    
    public LlmLog SetAdditionalMetadata(string? additionalMetadata)
    {
        AdditionalMetadata = additionalMetadata;
        return this;
    }
    
    // Helper method to calculate cost (if needed for billing)
    public decimal CalculateEstimatedCost(decimal promptTokenRate = 0.00001m, decimal responseTokenRate = 0.00001m)
    {
        return (PromptTokenCount * promptTokenRate) + (CandidatesTokenCount * responseTokenRate);
    }
}

// Domain Service Interface
public interface ILlmLogRepository : IRepository<LlmLog, Guid>
{
    Task<IEnumerable<LlmLog>> GetByPluginAsync(string pluginName, CancellationToken cancellationToken = default);
    Task<IEnumerable<LlmLog>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<LlmLog>> GetBySessionAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<IEnumerable<LlmLog>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IEnumerable<LlmLog>> GetFailedCallsAsync(CancellationToken cancellationToken = default);
    
    // Analytics methods
    Task<int> GetTotalTokenUsageAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
    Task<decimal> GetEstimatedCostAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
    Task<Dictionary<string, int>> GetUsageByPluginAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
    Task<Dictionary<string, int>> GetUsageByModelAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
}
