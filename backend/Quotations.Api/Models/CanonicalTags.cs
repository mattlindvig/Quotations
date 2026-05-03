namespace Quotations.Api.Models;

public static class CanonicalTags
{
    public static readonly IReadOnlyDictionary<string, string[]> ByCategory = new Dictionary<string, string[]>
    {
        ["Life & Human Experience"] = new[]
        {
            "life", "death", "aging", "childhood", "family", "friendship",
            "loneliness", "identity", "purpose", "happiness", "suffering",
            "loss", "gratitude"
        },
        ["Love & Relationships"] = new[]
        {
            "love", "heartbreak", "marriage", "betrayal", "trust", "forgiveness",
            "compassion", "kindness", "empathy"
        },
        ["Mind & Character"] = new[]
        {
            "wisdom", "courage", "integrity", "humility", "ambition", "patience",
            "perseverance", "self-knowledge", "discipline", "creativity"
        },
        ["Success & Failure"] = new[]
        {
            "success", "failure", "resilience", "risk", "opportunity", "work",
            "leadership", "excellence", "mediocrity"
        },
        ["Ideas & Knowledge"] = new[]
        {
            "truth", "reason", "doubt", "learning", "education", "books",
            "language", "art", "beauty", "music", "science", "technology"
        },
        ["Society & World"] = new[]
        {
            "politics", "power", "justice", "freedom", "equality", "war",
            "peace", "money", "progress", "history", "democracy", "tyranny"
        },
        ["Faith & Meaning"] = new[]
        {
            "faith", "religion", "god", "spirituality", "meaning", "morality",
            "ethics", "nature"
        },
        ["Tone & Style"] = new[]
        {
            "humor", "wit", "irony", "cynicism", "optimism", "pessimism",
            "poetic", "profound", "controversial"
        }
    };

    public static readonly IReadOnlyList<string> All =
        ByCategory.Values.SelectMany(t => t).Distinct().OrderBy(t => t).ToArray();
}
