namespace Quotations.Api.Configuration;

/// <summary>
/// Singleton holding mutable runtime state for AI review.
/// Can be toggled at runtime via the dashboard API.
/// </summary>
public class AiReviewRuntimeSettings
{
    private volatile bool _autoProcessingEnabled;
    private volatile bool _autoEnqueueEnabled;

    public AiReviewRuntimeSettings(bool autoProcessingEnabled = true, bool autoEnqueueEnabled = false)
    {
        _autoProcessingEnabled = autoProcessingEnabled;
        _autoEnqueueEnabled = autoEnqueueEnabled;
    }

    public bool AutoProcessingEnabled
    {
        get => _autoProcessingEnabled;
        set => _autoProcessingEnabled = value;
    }

    public bool AutoEnqueueEnabled
    {
        get => _autoEnqueueEnabled;
        set => _autoEnqueueEnabled = value;
    }
}
