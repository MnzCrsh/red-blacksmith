namespace RedBlacksmith.EndToEnd;

public sealed record PacketBatchProcessingResult(Guid BatchId, int EventCount);