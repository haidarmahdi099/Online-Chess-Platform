using System.ComponentModel;
using System.Diagnostics;
using Analysis.Application.Exceptions;
using Analysis.Application.Interfaces;
using Analysis.Domain.Chess;
using Analysis.Domain.Evaluation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Analysis.Infrastructure.Engine;

// Talks to a Stockfish executable over the UCI protocol. A fresh process is
// started per request: it is simple, isolates failures, and cannot leak state
// between requests. Concurrency is capped by a semaphore.
public sealed class StockfishEngine : IChessEngine, IDisposable
{
    private static readonly TimeSpan HealthProbeCacheDuration = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan FailedHealthProbeCacheDuration = TimeSpan.FromSeconds(1);

    private readonly StockfishOptions _options;
    private readonly ILogger<StockfishEngine> _logger;
    private readonly SemaphoreSlim _slots;
    private readonly SemaphoreSlim _healthProbeLock = new(1, 1);
    private DateTimeOffset _lastHealthProbeAtUtc = DateTimeOffset.MinValue;
    private bool _lastHealthProbeResult;

    public StockfishEngine(
        IOptions<StockfishOptions> options,
        ILogger<StockfishEngine> logger)
    {
        _options = options.Value;
        _logger = logger;
        // Hard safety caps: deployment configuration may lower these limits,
        // but cannot accidentally raise the requested resource budget.
        _options.MaxConcurrentAnalyses = Math.Clamp(_options.MaxConcurrentAnalyses, 1, 2);
        _options.AnalysisTimeoutSeconds = Math.Clamp(_options.AnalysisTimeoutSeconds, 1, 20);
        _options.QueueTimeoutSeconds = Math.Clamp(_options.QueueTimeoutSeconds, 1, 30);
        _options.HashMb = Math.Clamp(_options.HashMb, 16, 64);
        _options.Threads = Math.Clamp(_options.Threads, 1, 1);
        _slots = new SemaphoreSlim(_options.MaxConcurrentAnalyses);
    }

    public async Task<PositionEvaluation> EvaluateAsync(
        FenPosition position,
        int depth,
        CancellationToken cancellationToken)
    {
        var acquired = await _slots.WaitAsync(
            TimeSpan.FromSeconds(_options.QueueTimeoutSeconds),
            cancellationToken);

        if (!acquired)
        {
            throw new EngineUnavailableException(
                "The analysis engine is busy. Try again shortly.");
        }

        try
        {
            return await RunAnalysisAsync(position, depth, cancellationToken);
        }
        finally
        {
            _slots.Release();
        }
    }

    public async Task<bool> IsAvailableAsync(
        CancellationToken cancellationToken)
    {
        await _healthProbeLock.WaitAsync(cancellationToken);
        try
        {
            var cacheDuration = _lastHealthProbeResult
                ? HealthProbeCacheDuration
                : FailedHealthProbeCacheDuration;
            if (DateTimeOffset.UtcNow - _lastHealthProbeAtUtc < cacheDuration)
                return _lastHealthProbeResult;

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            using var session = await StartSessionAsync(timeout.Token);
            _lastHealthProbeAtUtc = DateTimeOffset.UtcNow;
            _lastHealthProbeResult = true;
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex) when (ex is EngineUnavailableException or OperationCanceledException)
        {
            _lastHealthProbeAtUtc = DateTimeOffset.UtcNow;
            _lastHealthProbeResult = false;
            return false;
        }
        finally
        {
            _healthProbeLock.Release();
        }
    }

    private async Task<PositionEvaluation> RunAnalysisAsync(
        FenPosition position,
        int depth,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource
            .CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(
            TimeSpan.FromSeconds(Math.Clamp(_options.AnalysisTimeoutSeconds, 1, 20)));

        try
        {
            using var session = await StartSessionAsync(timeout.Token);

            await session.SendAsync($"position fen {position.Fen}");

            // Ask Stockfish to display the board and determine whether
            // the side to move is in check.
            await session.SendAsync("d");

            bool sideToMoveInCheck = false;

            while (true)
            {
                var line = await session.ReadLineAsync(timeout.Token);

                if (line.StartsWith("Checkers:", StringComparison.OrdinalIgnoreCase))
                {
                    sideToMoveInCheck = !string.IsNullOrWhiteSpace(
                        line["Checkers:".Length..].Trim());
                    break;
                }
            }

            await session.SendAsync($"go depth {depth}");

            UciInfo? lastInfo = null;
            string? bestMove = null;

            while (true)
            {
                var line = await session.ReadLineAsync(timeout.Token);

                if (line.StartsWith("bestmove", StringComparison.Ordinal))
                {
                    var parts = line.Split(
                        ' ', StringSplitOptions.RemoveEmptyEntries);

                    bestMove = parts.Length > 1 &&
                               parts[1] is not ("(none)" or "0000")
                        ? parts[1]
                        : null;
                    break;
                }

                var info = UciInfoParser.TryParse(line);
                if (info is not null)
                    lastInfo = info;
            }

            var scoreType = lastInfo?.ScoreType ?? ScoreType.Centipawns;
            var score = lastInfo?.Score ?? 0;
            var evaluatedDepth = lastInfo?.Depth ?? 0;
            var principalVariation = lastInfo?.PrincipalVariation ?? [];

            // No legal move + in check means checkmate.
            // The side to move has lost, so its score is -1 in mate units.
            if (bestMove is null && sideToMoveInCheck)
            {
                scoreType = ScoreType.Mate;
                score = -1;
            }

            return new PositionEvaluation(
                scoreType,
                score,
                evaluatedDepth,
                bestMove,
                principalVariation);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Engine analysis timed out after {Seconds}s.",
                Math.Clamp(_options.AnalysisTimeoutSeconds, 1, 20));

            throw new EngineUnavailableException(
                "The analysis timed out. Try a lower depth.");
        }
    }
    private async Task<UciSession> StartSessionAsync(
        CancellationToken cancellationToken)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _options.Path,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            process.Start();
        }
        catch (Exception ex) when (
            ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            process.Dispose();
            _logger.LogError(
                ex, "Could not start engine at '{Path}'.", _options.Path);

            throw new EngineUnavailableException(
                "The analysis engine is not available.", ex);
        }

        var session = new UciSession(process, cancellationToken);

        try
        {
            await session.SendAsync("uci");
            await session.WaitForAsync("uciok", cancellationToken);

            await session.SendAsync(
                $"setoption name Threads value {Math.Clamp(_options.Threads, 1, 2)}");
            await session.SendAsync(
                $"setoption name Hash value {Math.Clamp(_options.HashMb, 1, 64)}");

            await session.SendAsync("isready");
            await session.WaitForAsync("readyok", cancellationToken);

            return session;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        _slots.Dispose();
        _healthProbeLock.Dispose();
    }

    private sealed class UciSession : IDisposable
    {
        private readonly Process _process;
        private readonly CancellationToken _cancellationToken;
        private readonly CancellationTokenRegistration _killOnCancel;

        public UciSession(Process process, CancellationToken cancellationToken)
        {
            _process = process;
            _cancellationToken = cancellationToken;

            // Reads on a process pipe don't reliably honour cancellation, so
            // cancelling (timeout or client abort) kills the engine instead.
            _killOnCancel = cancellationToken.Register(Kill);
        }

        public async Task SendAsync(string command)
        {
            try
            {
                await _process.StandardInput.WriteLineAsync(command);
                await _process.StandardInput.FlushAsync();
            }
            catch (IOException ex)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                throw new EngineUnavailableException(
                    "The analysis engine stopped unexpectedly.", ex);
            }
        }

        public async Task<string> ReadLineAsync(
            CancellationToken cancellationToken)
        {
            var line = await _process.StandardOutput
                .ReadLineAsync(cancellationToken);

            if (line is null)
            {
                // End of stream: either we killed the engine on cancellation,
                // or it crashed.
                cancellationToken.ThrowIfCancellationRequested();

                throw new EngineUnavailableException(
                    "The analysis engine stopped unexpectedly.");
            }

            return line;
        }

        public async Task WaitForAsync(
            string expected,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                var line = await ReadLineAsync(cancellationToken);

                if (line.Trim() == expected)
                    return;
            }
        }

        private void Kill()
        {
            try
            {
                if (!_process.HasExited)
                    _process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Already exited or disposed.
            }
        }

        public void Dispose()
        {
            _killOnCancel.Dispose();
            Kill();
            _process.Dispose();
        }
    }
}
