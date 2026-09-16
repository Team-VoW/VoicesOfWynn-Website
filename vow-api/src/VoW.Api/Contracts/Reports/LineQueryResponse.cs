namespace VoW.Api.Contracts.Reports;

public sealed record LineQueryResponse(
    int Total,
    IReadOnlyList<LineResponse> Results);

public sealed record LineResponse(
    string ChatMessage,
    string? NpcName,
    PositionResponse? Position);

public sealed record PositionResponse(int X, int Y, int Z);
