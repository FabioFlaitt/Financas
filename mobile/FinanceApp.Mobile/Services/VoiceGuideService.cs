using Microsoft.Maui.Media;

namespace FinanceApp.Mobile.Services;

/// <summary>
/// Fala os avisos da viagem (pedágio, curvas) usando o text-to-speech nativo do aparelho.
/// Grátis e offline. Uma fala nova interrompe a anterior para o aviso ser sempre o mais atual.
/// </summary>
public class VoiceGuideService
{
    private Locale? _ptBr;
    private bool _localeLoaded;
    private CancellationTokenSource? _cts;

    public async Task EnsureLocaleAsync()
    {
        if (_localeLoaded) return;
        _localeLoaded = true;
        try
        {
            var locales = await TextToSpeech.Default.GetLocalesAsync();
            _ptBr = locales.FirstOrDefault(l => l.Language.StartsWith("pt", StringComparison.OrdinalIgnoreCase)
                                                && l.Country.Contains("BR", StringComparison.OrdinalIgnoreCase))
                    ?? locales.FirstOrDefault(l => l.Language.StartsWith("pt", StringComparison.OrdinalIgnoreCase));
        }
        catch { /* sem locales disponíveis: usa a voz padrão do sistema */ }
    }

    /// <summary>Fala o texto (não bloqueia). Cancela a fala anterior em andamento.</summary>
    public void Speak(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var options = _ptBr is not null ? new SpeechOptions { Locale = _ptBr } : null;

        _ = Task.Run(async () =>
        {
            try { await TextToSpeech.Default.SpeakAsync(text, options, token); }
            catch { /* interrompida ou TTS indisponível */ }
        });
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
    }
}
