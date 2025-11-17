namespace RagAgentApi.Models;

// Conversations
public record ConversationDto(
    Guid Id,
    string? Title,
    DateTime CreatedAt,
    DateTime LastMessageAt,
    int MessageCount
);

public record MessageDto(
    Guid Id,
    string Role,
    string Content,
    DateTime CreatedAt,
    List<SourceDto>? Sources
);

public record CreateConversationResponse(Guid Id);

// Query Response
public record QueryResponse(
    string Answer,
    List<SourceDto> Sources
);

public record SourceDto(
    string Url,
    string Content,
    double RelevanceScore
);

// Agent Analytics
public record AgentStatsResponse(
    List<AgentStatDto> Stats
);

public record AgentStatDto(
    string AgentName,
    int ExecutionCount,
    double AvgDurationMs,
    double SuccessRate
);
