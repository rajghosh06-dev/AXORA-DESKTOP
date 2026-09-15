using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.SpeechRecognition;
using Microsoft.Extensions.Logging;
using Axora.Desktop.Models.Voice;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Voice dictation service using Windows.Media.SpeechRecognition.
/// Treats Windows speech recognition as an environment-dependent capability,
/// gracefully handles permission denial (0x80070005), missing language resources,
/// and deterministically disposes WinRT handles across start/stop cycles.
/// </summary>
public sealed class VoiceTranscriberService : IVoiceTranscriberService, IDisposable
{
    private readonly ILogger<VoiceTranscriberService>? _logger;
    private readonly SemaphoreSlim _startStopLock = new(1, 1);
    private SpeechRecognizer? _recognizer;
    private Action<TranscriptionChunk>? _chunkCallback;
    private Action<string>? _stringCallback;
    private volatile bool _isRecording;
    private AudioCaptureHealth _deviceHealth = AudioCaptureHealth.Healthy;

    public bool IsRecording => _isRecording;
    public AudioCaptureHealth DeviceHealth => _deviceHealth;

    public event EventHandler<VoiceTranscriberStateChangedEventArgs>? StateChanged;

    public VoiceTranscriberService(ILogger<VoiceTranscriberService>? logger = null)
    {
        _logger = logger;
    }

    public async Task<bool> CheckPrerequisitesAsync(CancellationToken ct = default)
    {
        try
        {
            var systemLanguage = SpeechRecognizer.SystemSpeechLanguage;
            bool isSupported = SpeechRecognizer.SupportedTopicLanguages.Contains(systemLanguage);
            if (!isSupported)
            {
                _deviceHealth = AudioCaptureHealth.RecognitionUnavailable;
                _logger?.LogWarning("System speech language {Lang} is not in SupportedTopicLanguages.", systemLanguage.DisplayName);
                return false;
            }
            _deviceHealth = AudioCaptureHealth.Healthy;
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _deviceHealth = AudioCaptureHealth.PermissionDenied;
            return false;
        }
        catch (Exception ex) when ((uint)ex.HResult == 0x80070005)
        {
            _deviceHealth = AudioCaptureHealth.PermissionDenied;
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to check speech recognition prerequisites.");
            _deviceHealth = AudioCaptureHealth.RecognitionUnavailable;
            return false;
        }
    }

    public Task StartDictationAsync(Action<string> onTextRecognized, CancellationToken ct = default)
    {
        _stringCallback = onTextRecognized;
        return StartDictationInternalAsync(ct);
    }

    public Task StartDictationAsync(Action<TranscriptionChunk> onChunkRecognized, CancellationToken ct = default)
    {
        _chunkCallback = onChunkRecognized;
        return StartDictationInternalAsync(ct);
    }

    private async Task StartDictationInternalAsync(CancellationToken ct = default)
    {
        await _startStopLock.WaitAsync(ct);
        try
        {
            if (_isRecording) return;

            // R-VOICE-14: Explicitly dispose previous recognizer before creating a new one
            // to release native WASAPI microphone capture handles.
            DisposeRecognizer();

            var language = SpeechRecognizer.SystemSpeechLanguage;
            _recognizer = new SpeechRecognizer(language);
            var topicConstraint = new SpeechRecognitionTopicConstraint(
                SpeechRecognitionScenario.Dictation, "Dictation");
            _recognizer.Constraints.Add(topicConstraint);

            var compilationResult = await _recognizer.CompileConstraintsAsync();
            if (compilationResult.Status != SpeechRecognitionResultStatus.Success)
            {
                _deviceHealth = AudioCaptureHealth.RecognitionUnavailable;
                _logger?.LogWarning("SpeechRecognizer constraint compilation failed: {Status}", compilationResult.Status);
                DisposeRecognizer();
                StateChanged?.Invoke(this, new VoiceTranscriberStateChangedEventArgs(false, _deviceHealth));
                return;
            }

            _recognizer.ContinuousRecognitionSession.ResultGenerated += OnResultGenerated;
            _recognizer.ContinuousRecognitionSession.Completed += OnSessionCompleted;

            await _recognizer.ContinuousRecognitionSession.StartAsync();
            _isRecording = true;
            _deviceHealth = AudioCaptureHealth.Healthy;
            _logger?.LogInformation("Voice dictation session started (Language: {Lang})", language.DisplayName);
            StateChanged?.Invoke(this, new VoiceTranscriberStateChangedEventArgs(true, _deviceHealth));
        }
        catch (UnauthorizedAccessException ex)
        {
            _isRecording = false;
            _deviceHealth = AudioCaptureHealth.PermissionDenied;
            DisposeRecognizer();
            _logger?.LogWarning(ex, "Microphone access denied during dictation start (0x80070005).");
            StateChanged?.Invoke(this, new VoiceTranscriberStateChangedEventArgs(false, _deviceHealth));
        }
        catch (Exception ex) when ((uint)ex.HResult == 0x80070005)
        {
            _isRecording = false;
            _deviceHealth = AudioCaptureHealth.PermissionDenied;
            DisposeRecognizer();
            _logger?.LogWarning(ex, "Microphone access denied (0x80070005).");
            StateChanged?.Invoke(this, new VoiceTranscriberStateChangedEventArgs(false, _deviceHealth));
        }
        catch (Exception ex)
        {
            _isRecording = false;
            _deviceHealth = AudioCaptureHealth.RecognitionUnavailable;
            DisposeRecognizer();
            _logger?.LogWarning(ex, "Failed to start speech dictation session.");
            StateChanged?.Invoke(this, new VoiceTranscriberStateChangedEventArgs(false, _deviceHealth));
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

            try
            {
                // Detach handler before stopping to prevent race conditions with disposed state
                _recognizer.ContinuousRecognitionSession.ResultGenerated -= OnResultGenerated;
                _recognizer.ContinuousRecognitionSession.Completed -= OnSessionCompleted;

                await _recognizer.ContinuousRecognitionSession.StopAsync();
                _logger?.LogInformation("Voice dictation session stopped.");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error stopping continuous speech recognition session.");
            }
            finally
            {
                _isRecording = false;
                DisposeRecognizer();
                StateChanged?.Invoke(this, new VoiceTranscriberStateChangedEventArgs(false, _deviceHealth));
            }
        }
        finally
        {
            _startStopLock.Release();
        }
    }

    private void OnResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
    {
        string text = args.Result.Text;
        if (!string.IsNullOrWhiteSpace(text))
        {
            _stringCallback?.Invoke(text);
            _chunkCallback?.Invoke(new TranscriptionChunk(
                RawText: text,
                FormattedText: text,
                IsFinal: args.Result.Status == SpeechRecognitionResultStatus.Success
            ));
        }
    }

    private void OnSessionCompleted(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionCompletedEventArgs args)
    {
        _isRecording = false;
        _logger?.LogInformation("Continuous recognition session completed. Status: {Status}", args.Status);
        StateChanged?.Invoke(this, new VoiceTranscriberStateChangedEventArgs(false, _deviceHealth));
    }

    private void DisposeRecognizer()
    {
        if (_recognizer != null)
        {
            try
            {
                _recognizer.ContinuousRecognitionSession.ResultGenerated -= OnResultGenerated;
                _recognizer.ContinuousRecognitionSession.Completed -= OnSessionCompleted;
                _recognizer.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error disposing SpeechRecognizer instance.");
            }
            finally
            {
                _recognizer = null;
            }
        }
    }

    public void Dispose()
    {
        _isRecording = false;
        DisposeRecognizer();
        _startStopLock.Dispose();
    }
}
