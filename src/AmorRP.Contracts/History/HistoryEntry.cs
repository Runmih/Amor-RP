namespace AmorRP.Contracts.History;

public sealed record HistoryEntry(Guid Id, Guid OperationId, string Kind, Guid ActorCharacterId, Guid? SubjectCharacterId, DateTimeOffset OccurredAt, string Summary, string? Reason);
