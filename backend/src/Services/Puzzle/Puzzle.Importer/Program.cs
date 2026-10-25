using CsvHelper;
using CsvHelper.Configuration;
using Game.Domain.Chess;
using Game.Infrastructure.Chess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Puzzle.Infrastructure.Persistence;
using System.Globalization;
using ZstdSharp;
using PuzzleEntity = Puzzle.Domain.Entities.Puzzle;

const int BatchSize = 1000;

var maxRows = args.Length > 0 && int.TryParse(args[0], out var parsed)
    ? parsed
    : 10000;

if (maxRows < 0)
{
    Console.Error.WriteLine("Maximum rows must be zero or greater.");
    return;
}

Console.Write("Path to lichess_db_puzzle.csv.zst: ");
var filePath = Console.ReadLine()?.Trim().Trim('"');

if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
{
    Console.Error.WriteLine("Dataset file not found.");
    return;
}

var apiDirectory = FindApiDirectory();

var configuration = new ConfigurationBuilder()
    .SetBasePath(apiDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

// appsettings.json has no password; the env var (set by scripts/Load-Env.ps1) takes precedence.
var connectionString =
    Environment.GetEnvironmentVariable("ConnectionStrings__PuzzleDatabase")
    ?? configuration.GetConnectionString("PuzzleDatabase");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:PuzzleDatabase is missing.");
}

var options = new DbContextOptionsBuilder<PuzzleDbContext>()
    .UseNpgsql(connectionString)
    .Options;

using var db = new PuzzleDbContext(options);

var chessRules = new PaxChessRules();

var batch = new List<PuzzleEntity>(BatchSize);
var rowsRead = 0;
var inserted = 0;
var skipped = 0;

await using var compressedFile = File.OpenRead(filePath);
await using var decompressed = new DecompressionStream(compressedFile);
using var textReader = new StreamReader(decompressed);

var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    HasHeaderRecord = true,
    MissingFieldFound = null
};

using var csv = new CsvReader(textReader, csvConfig);

await foreach (var row in csv.GetRecordsAsync<LichessPuzzleRow>())
{
    rowsRead++;

    var moves = row.Moves.Split(
        ' ',
        StringSplitOptions.RemoveEmptyEntries);

    if (string.IsNullOrWhiteSpace(row.PuzzleId) ||
        string.IsNullOrWhiteSpace(row.FEN) ||
        moves.Length < 2 ||
        row.Rating <= 0)
    {
        skipped++;
        continue;
    }

    try
    {
        var position = ChessPosition.FromFen(row.FEN);
        string? puzzleFen = null;
        var lineIsLegal = true;

        // Lichess stores the opponent's setup move first. Apply the whole line to
        // validate it, but persist the position after that setup move and only the
        // moves the player is expected to solve.
        for (var moveIndex = 0; moveIndex < moves.Length; moveIndex++)
        {
            var uci = moves[moveIndex];
            if (uci.Length is not (4 or 5))
            {
                lineIsLegal = false;
                break;
            }

            var chessMove = ChessMove.Create(
                uci[..2],
                uci[2..4],
                uci.Length == 5 ? uci[4..] : null);
            var result = chessRules.ValidateAndApply(position, chessMove);
            if (!result.IsLegal || string.IsNullOrWhiteSpace(result.NewFen))
            {
                lineIsLegal = false;
                break;
            }

            if (moveIndex == 0)
                puzzleFen = result.NewFen;

            position = ChessPosition.FromFen(result.NewFen);
        }

        if (!lineIsLegal || string.IsNullOrWhiteSpace(puzzleFen))
        {
            skipped++;
            continue;
        }

        batch.Add(new PuzzleEntity
        {
            ExternalPuzzleId = row.PuzzleId,
            Fen = puzzleFen,
            SolutionMoves = string.Join(' ', moves.Skip(1)),
            Rating = row.Rating,
            Themes = string.Join(
                ',',
                (row.Themes ?? "").Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries)),
            Source = "Lichess puzzle database (CC0)",
            IsActive = true
        });
    }
    catch (Exception ex) when (
        ex is ArgumentException ||
        ex is InvalidOperationException ||
        ex is FormatException ||
        ex is IndexOutOfRangeException)
    {
        skipped++;
        continue;
    }

    if (batch.Count >= BatchSize)
    {
        var result = await ImportBatchAsync(db, batch);
        inserted += result.Inserted;
        skipped += result.Skipped;

        Console.WriteLine(
            $"Read {rowsRead:N0}; inserted {inserted:N0}; skipped {skipped:N0}");

        batch.Clear();
    }

    if (maxRows > 0 && rowsRead >= maxRows)
        break;
}

if (batch.Count > 0)
{
    var result = await ImportBatchAsync(db, batch);
    inserted += result.Inserted;
    skipped += result.Skipped;
}

Console.WriteLine();
Console.WriteLine("Import finished.");
Console.WriteLine($"Rows read: {rowsRead:N0}");
Console.WriteLine($"Inserted: {inserted:N0}");
Console.WriteLine($"Skipped: {skipped:N0}");

static async Task<(int Inserted, int Skipped)> ImportBatchAsync(
    PuzzleDbContext db,
    List<PuzzleEntity> batch)
{
    var ids = batch
        .Select(p => p.ExternalPuzzleId!)
        .Distinct()
        .ToArray();

    var existingIds = await db.Puzzles
        .AsNoTracking()
        .Where(p => p.ExternalPuzzleId != null &&
                    ids.Contains(p.ExternalPuzzleId))
        .Select(p => p.ExternalPuzzleId!)
        .ToListAsync();

    var existing = existingIds.ToHashSet(StringComparer.Ordinal);

    var newPuzzles = batch
        .Where(p => !existing.Contains(p.ExternalPuzzleId!))
        .GroupBy(p => p.ExternalPuzzleId, StringComparer.Ordinal)
        .Select(group => group.First())
        .ToList();

    var skippedCount = batch.Count - newPuzzles.Count;

    db.Puzzles.AddRange(newPuzzles);
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();

    return (newPuzzles.Count, skippedCount);
}

static string FindApiDirectory()
{
    for (var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
         directory is not null;
         directory = directory.Parent)
    {
        var candidate = Path.Combine(
            directory.FullName,
            "Puzzle.Api",
            "appsettings.json");

        if (File.Exists(candidate))
            return Path.GetDirectoryName(candidate)!;
    }

    throw new DirectoryNotFoundException(
        "Could not find Puzzle.Api/appsettings.json.");
}

public sealed class LichessPuzzleRow
{
    public string PuzzleId { get; set; } = "";
    public string FEN { get; set; } = "";
    public string Moves { get; set; } = "";
    public int Rating { get; set; }
    public string Themes { get; set; } = "";
}
