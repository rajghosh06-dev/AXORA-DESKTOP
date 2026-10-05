using System.Text;

namespace Axora.Studio.Services.Contracts;

public enum ReadAloudOutcome { Completed, Canceled, Replaced, Unavailable, InvalidText, SynthesisFailed, PlaybackFailed }
public enum ReadAloudCleanup { Released, Deferred }
public enum ReadAloudPhase { Idle, Preparing, Reading, Stopping, Finished, Unavailable, Failed }
public sealed record ReadAloudRequest(Guid OperationId, string Text);
public sealed record ReadAloudProgress(Guid OperationId, ReadAloudPhase Phase, string ReasonCode = "");
public sealed record ReadAloudResult(Guid OperationId, ReadAloudOutcome Outcome, string ReasonCode,
    ReadAloudCleanup Cleanup = ReadAloudCleanup.Released);

public interface IFlashcardReadAloudService
{
    bool IsActive { get; }
    Task<ReadAloudResult> ReadAsync(ReadAloudRequest request, Action<ReadAloudProgress>? progress = null);
    Task<ReadAloudResult> CancelCurrentAsync();
    Task<ReadAloudResult> StopAsync();
}

/// <summary>One invocation owns its native objects until the returned task establishes release.</summary>
public interface IFlashcardReadAloudBackend
{
    Task<ReadAloudResult> RunAsync(ReadAloudRequest request, CancellationToken cancellation,
        Func<bool> mayPlay, Action<ReadAloudProgress> progress);
}

public static class ReadAloudText
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    public static bool IsValid(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 8192) return false;
        try { StrictUtf8.GetByteCount(text); return text.EnumerateRunes().Count() <= 4096; }
        catch (EncoderFallbackException) { return false; }
    }
}
