using System.Globalization;
using CommunityToolkit.Maui.Media;

namespace FinanceApp.Mobile.Services;

/// <summary>
/// Fachada do reconhecimento de voz. Isola o <see cref="SpeechToText"/> do Community Toolkit do restante do app
/// (facilita testes e deixa o fluxo "ditar o gasto" cego para a implementação de cada plataforma).
/// </summary>
public interface ISpeechToTextService
{
    Task<bool> RequestPermissionAsync(CancellationToken ct = default);
    Task<string> ListenAsync(CultureInfo culture, Action<string>? onPartial = null, CancellationToken ct = default);
    Task CancelAsync();
}

public sealed class SpeechToTextService : ISpeechToTextService
{
    public async Task<bool> RequestPermissionAsync(CancellationToken ct = default)
    {
        try { return await SpeechToText.Default.RequestPermissions(ct); }
        catch { return false; }
    }

    /// <summary>Escuta até o aparelho detectar silêncio ou o usuário tocar de novo no botão. Devolve o texto final.</summary>
    public async Task<string> ListenAsync(CultureInfo culture, Action<string>? onPartial = null, CancellationToken ct = default)
    {
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lastPartial = string.Empty;

        void OnPartial(object? sender, SpeechToTextRecognitionResultUpdatedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.RecognitionResult))
            {
                lastPartial = e.RecognitionResult;
                onPartial?.Invoke(e.RecognitionResult);
            }
        }

        void OnDone(object? sender, SpeechToTextRecognitionResultCompletedEventArgs e)
        {
            var text = e.RecognitionResult.Text?.Trim();
            completion.TrySetResult(!string.IsNullOrEmpty(text) ? text : lastPartial.Trim());
        }

        SpeechToText.Default.RecognitionResultUpdated += OnPartial;
        SpeechToText.Default.RecognitionResultCompleted += OnDone;
        try
        {
            await SpeechToText.Default.StartListenAsync(new SpeechToTextOptions { Culture = culture, ShouldReportPartialResults = true }, ct);
            using var _ = ct.Register(() => completion.TrySetResult(lastPartial.Trim()));
            return await completion.Task;
        }
        finally
        {
            SpeechToText.Default.RecognitionResultUpdated -= OnPartial;
            SpeechToText.Default.RecognitionResultCompleted -= OnDone;
            try { await SpeechToText.Default.StopListenAsync(CancellationToken.None); } catch { }
        }
    }

    public Task CancelAsync()
    {
        try { return SpeechToText.Default.StopListenAsync(CancellationToken.None); }
        catch { return Task.CompletedTask; }
    }
}
