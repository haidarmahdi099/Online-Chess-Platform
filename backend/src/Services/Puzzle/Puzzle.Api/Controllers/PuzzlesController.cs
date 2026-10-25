using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Puzzle.Application.DTOs;
using Puzzle.Application.Services;
using System.Security.Claims;
using Puzzle.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Puzzle.Api.Controllers;

[ApiController]
[Route("puzzles")]
public sealed class PuzzlesController(IPuzzleService puzzleService, PuzzleDbContext db)
    : ControllerBase
{
    [HttpGet("next")]
    [AllowAnonymous]
    [ProducesResponseType<Puzzle.Application.DTOs.PuzzleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNext(
        [FromQuery] Guid? excludePuzzleId,
        CancellationToken cancellationToken)
    {
        var puzzle = await puzzleService.GetNextAsync(
            excludePuzzleId, cancellationToken);

        return puzzle is null ? NotFound() : Ok(puzzle);
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType<PuzzlePageDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPuzzles(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? minRating = null,
        [FromQuery] int? maxRating = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidPagination(page, pageSize) ||
            minRating < 0 ||
            maxRating < 0 ||
            (minRating.HasValue && maxRating.HasValue &&
             minRating.Value > maxRating.Value))
        {
            return BadRequest(new
            {
                message = "Invalid pagination or rating filters."
            });
        }

        var result = await puzzleService.GetPuzzlesAsync(
            page, pageSize, minRating, maxRating, cancellationToken);

        return Ok(result);
    }

    /// <summary>Play a puzzle one move at a time; see IPuzzleService.SubmitStepAsync.</summary>
    [HttpPost("{id:guid}/step")]
    [Authorize]
    [ProducesResponseType<PuzzleStepResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitStep(
        Guid id,
        [FromBody] SubmitPuzzleAttemptDto request,
        CancellationToken cancellationToken)
    {
        var userIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdValue, out var userId))
            return Unauthorized();

        if (request.Moves is null ||
            request.Moves.Length is < 1 or > 50 ||
            request.Moves.Any(string.IsNullOrWhiteSpace))
        {
            return BadRequest(new
            {
                message = "Submit between 1 and 50 non-empty UCI moves (your moves only)."
            });
        }

        var result = await puzzleService.SubmitStepAsync(
            id, userId, request.Moves, cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/attempt")]
    [Authorize]
    [ProducesResponseType<PuzzleAttemptResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitAttempt(
        Guid id,
        [FromBody] SubmitPuzzleAttemptDto request,
        CancellationToken cancellationToken)
    {
        var userIdValue =
            User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdValue, out var userId))
            return Unauthorized();

        if (request.Moves is null ||
            request.Moves.Length is < 1 or > 100 ||
            request.Moves.Any(string.IsNullOrWhiteSpace))
        {
            return BadRequest(new
            {
                message = "Submit between 1 and 100 non-empty UCI moves."
            });
        }

        var result = await puzzleService.SubmitAttemptAsync(
            id, userId, request.Moves, cancellationToken);

        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("me/stats")]
    [Authorize]
    public async Task<IActionResult> MyStats(CancellationToken ct)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var attempts = db.PuzzleAttempts.AsNoTracking().Where(x => x.UserId == userId);
        var total = await attempts.CountAsync(ct);
        var solved = await attempts.CountAsync(x => x.IsCorrect, ct);
        var distinctSolved = await attempts.Where(x => x.IsCorrect).Select(x => x.PuzzleId).Distinct().CountAsync(ct);
        var lastAttempt = await attempts.OrderByDescending(x => x.AttemptedAtUtc).Select(x => (DateTime?)x.AttemptedAtUtc).FirstOrDefaultAsync(ct);
        var solvedDayTimestamps = await attempts
            .Where(x => x.IsCorrect)
            .Select(x => x.AttemptedAtUtc.Date)
            .Distinct()
            .OrderByDescending(x => x)
            .ToListAsync(ct);
        var solvedDays = solvedDayTimestamps
            .Select(DateOnly.FromDateTime)
            .ToArray();
        var streak = 0;
        if (solvedDays.Length > 0)
        {
            var expected = DateOnly.FromDateTime(DateTime.UtcNow);
            if (solvedDays[0] < expected) expected = expected.AddDays(-1);
            foreach (var day in solvedDays) { if (day != expected) break; streak++; expected = expected.AddDays(-1); }
        }
        return Ok(new { userId, totalAttempts = total, correctAttempts = solved, accuracy = total == 0 ? 0 : Math.Round((double)solved / total * 100, 2), distinctPuzzlesSolved = distinctSolved, currentDailyStreak = streak, lastAttemptAtUtc = lastAttempt });
    }

    [HttpGet("me/history")]
    [Authorize]
    public async Task<IActionResult> MyHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        if (!IsValidPagination(page, pageSize)) return BadRequest(new { code = "invalid_pagination" });
        var query = db.PuzzleAttempts.AsNoTracking().Where(x => x.UserId == userId)
            .OrderByDescending(x => x.AttemptedAtUtc)
            .ThenByDescending(x => x.Id);
        var totalCount = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new { id = x.Id, puzzleId = x.PuzzleId, isCorrect = x.IsCorrect, attemptedAtUtc = x.AttemptedAtUtc, submittedMoves = x.SubmittedMoves })
            .ToListAsync(ct);
        return Ok(new { items, page, pageSize, totalCount, totalPages = (int)Math.Ceiling(totalCount / (double)pageSize) });
    }

    private bool TryGetUserId(out Guid userId)
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(raw, out userId);
    }

    private static bool IsValidPagination(int page, int pageSize) =>
        page >= 1 &&
        pageSize is >= 1 and <= 100 &&
        (long)(page - 1) * pageSize <= int.MaxValue;

}
