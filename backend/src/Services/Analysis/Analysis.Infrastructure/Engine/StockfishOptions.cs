namespace Analysis.Infrastructure.Engine;

public sealed class StockfishOptions
{
    public const string SectionName = "Engine";

    // Path to the Stockfish executable, or its name if it is on PATH.
    public string Path { get; set; } = "stockfish";

    public int Threads { get; set; } = 1;

    public int HashMb { get; set; } = 64;

    // Maximum number of engine processes running at once.
    public int MaxConcurrentAnalyses { get; set; } = 2;

    // How long a request may wait for a free engine slot.
    public int QueueTimeoutSeconds { get; set; } = 5;

    // Hard limit for a single analysis; the process is killed afterwards.
    public int AnalysisTimeoutSeconds { get; set; } = 20;
}
