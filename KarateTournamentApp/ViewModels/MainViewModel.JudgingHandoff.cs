using System.IO;
using System.Text.Json;
using KarateClassLibrary.Contracts;
using KarateTournamentApp.Services.Api;

namespace KarateTournamentApp.ViewModels;

public partial class MainViewModel
{
    private bool _checkingHandoffs;
    private JudgingSession? _pendingHandoff;

    internal async Task CheckJudgingHandoffsAsync(CancellationToken cancellationToken)
    {
        if (_api is null || _sessions is null || IsLoading || _checkingHandoffs)
            return;

        _checkingHandoffs = true;
        try
        {
            var candidates = _activeJudgeSession is { } active ? [active]
                : JudgingSessions.Where(session => session.Category is not null).ToArray();
            foreach (var candidate in candidates)
            {
                var state = await _api.GetHandoffStateAsync(candidate.CategoryId, candidate.Token, cancellationToken);
                if (IsLoading || cancellationToken.IsCancellationRequested)
                    return;
                if (!state.Requested && !state.Released && !candidate.AwaitingHandoff)
                    continue;
                if (_activeJudgeSession?.Token == candidate.Token)
                {
                    // Closing stops the clock and captures the latest notes before we ask the API to release the claim.
                    _pendingHandoff = _activeJudgeSession;
                    _pendingHandoff.AwaitingHandoff = true;
                    _activeJudgeWindow!.Close();
                }
                if (_activeJudgeLock is null)
                    await SaveHandoffAsync(candidate, state.Released, cancellationToken);
                return;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception error) when (error is InvalidOperationException or IOException or JsonException or UnauthorizedAccessException)
        {
            if (_pendingHandoff is { } pending)
                JudgingWarning = $"Nie udało się jeszcze przekazać konkurencji „{pending.Name}”. {error.Message}";
        }
        finally { _checkingHandoffs = false; }
    }

    private async Task SaveHandoffAsync(JudgingSession selected, bool alreadyReleased, CancellationToken cancellationToken)
    {
        using var heldLock = _sessions!.Lock(selected.CategoryId);
        IsLoading = true;
        try
        {
            var stored = _sessions.Load(selected.CategoryId);
            if (stored?.Token != selected.Token)
                return;
            var session = _pendingHandoff?.Token == selected.Token ? _pendingHandoff : stored;
            if (!alreadyReleased)
            {
                if (session.Category is null)
                    await _api!.ReleaseCategoryAsync(session.CategoryId, new ReleaseCategoryRequest(session.Token));
                else
                {
                    _pendingHandoff = session;
                    session.AwaitingHandoff = true;
                    _sessions.Save(session);
                    var checkpoint = JudgingCheckpointMapper.Capture(session);
                    await _api!.SaveAndReleaseJudgingAsync(session.CategoryId, new(session.Token, checkpoint), cancellationToken);
                }
            }

            _sessions.Archive(session.CategoryId);
            if (_pendingHandoff?.Token == session.Token)
                _pendingHandoff = null;
            JudgingWarning = $"Zwolniono konkurencję „{session.Name}”.";
            ShowApiSnapshot(await _api!.GetSnapshotAsync(cancellationToken));
        }
        finally
        {
            IsLoading = false;
            RefreshJudgingSessions();
        }
    }
}
