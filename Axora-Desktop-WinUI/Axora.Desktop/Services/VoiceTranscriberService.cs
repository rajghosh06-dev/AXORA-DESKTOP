using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Globalization;
using Windows.Media.SpeechRecognition;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Voice Dictation &amp; Speech Transcriber using native Windows.Media.SpeechRecognition.
/// Captures microphone audio streams and transcribes speech into formatted text with punctuation.
///
/// Memory Safety: Previous SpeechRecognizer instance is always disposed before creating a new
/// one to prevent hardware microphone handle accumulation across start/stop cycles.
/// Locking: A SemaphoreSlim serializes concurrent start/stop calls to prevent race conditions.
/// </summary>
public sealed class VoiceTranscriberService : IVoiceTranscriberService, IDisposable
{
    private readonly ILogger<VoiceTranscriberService> _logger;
    private readonly SemaphoreSlim _startStopLock = new(1, 1);
    private SpeechRecognizer? _recognizer;
    private Action<string>? _callback;
    private volatile bool _isRecording;

    public bool IsRecording => _isRecording;

    public VoiceTranscriberService(ILogger<VoiceTranscriberService> logger)
    {
        _logger = logger;
    }

    public async Task StartDictationAsync(Action<string> onTextRecognized, CancellationToken ct = default)
    {
        await _startStopLock.WaitAsync(ct);
        try
        {
            if (_isRecording) return;

            // FIX W-4: Always dispose previous recognizer before allocating a new one.
            // Rapid start/stop cycles without this accumulate unmanaged WinRT microphone handles.
            _recognizer?.Dispose();
            _recognizer = null;

            _callback = onTextRecognized;

            _recognizer = new SpeechRecognizer(SpeechRecognizer.SystemSpeechLanguage);
            var topicConstraint = new SpeechRecognitionTopicConstraint(
                SpeechRecognitionScenario.Dictation, "Dictation");
            _recognizer.Constraints.Add(topicConstraint);

            var compilationResult = await _recognizer.CompileConstraintsAsync();
            if (compilationResult.Status != SpeechRecognitionResultStatus.Success)
            {
                _logger.LogWarning("SpeechRecognizer compilation failed: {Status}", compilationResult.Status);
                _recognizer.Dispose();
                _recognizer = null;
                return;
            }

            _recognizer.ContinuousRecognitionSession.ResultGenerated += OnResultGenerated;

            await _recognizer.ContinuousRecognitionSession.StartAsync();
            _isRecording = true;
            _logger.LogInformation("Voice dictation session started");
        }
        catch (Exception ex)
        {
            _isRecording = false;
            _recognizer?.Dispose();
            _recognizer = null;
            _logger.LogWarning(ex, "Failed to start speech dictation session");
        }
        finally
        {
            _startStopLock.Release();
        }
    }

    public async Task StopDictationAsync()
    {
        await _startStopLock.WaitAsync();
        try
        {
            if (!_isRecording || _recognizer == null) return;

            // Detach handler before stopping to prevent late callbacks against disposed resources
            _recognizer.ContinuousRecognitionSession.ResultGenerated -= OnResultGenerated;

            try
            {
                await _recognizer.ContinuousRecognitionSession.StopAsync();
                _logger.LogInformation("Voice dictation session stopped");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping speech dictation");
            }
            finally
            {
                _isRecording = false;
            }
        }
        finally
        {
            _startStopLock.Release();
        }
    }

    private void OnResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.Result.Text))
        {
            _callback?.Invoke(args.Result.Text);
        }
    }

    public void Dispose()
    {
        _isRecording = false;
        if (_recognizer is not null)
        {
            _recognizer.ContinuousRecognitionSession.ResultGenerated -= OnResultGenerated;
            _recognizer.Dispose();
            _recognizer = null;
        }
        _startStopLock.Dispose();
    }
}
