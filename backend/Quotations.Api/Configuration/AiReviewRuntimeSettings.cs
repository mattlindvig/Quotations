namespace Quotations.Api.Configuration;

/// <summary>
/// Singleton holding mutable runtime state for AI review.
/// Initialized from AiReviewOptions.Enabled at startup; can be toggled at runtime.
/// </summary>
public class AiReviewRuntimeSettings
{
    private volatile bool _autoProcessingEnabled;

    public AiReviewRuntimeSettings(bool initialEnabled = false)
    {
        _autoProcessingEnabled = initialEnabled;
    }

    public bool AutoProcessingEnabled
    {
        get => _autoProcessingEnabled;
        set => _autoProcessingEnabled = value;
    }
}
