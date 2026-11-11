namespace VirtoCommerce.SystemOperations.Core.Diagnostics;

// One thing a check found: what it is, how bad, the evidence it rests on, who fixes it and how.
public class Finding
{
    public string Id { get; set; }

    public string Severity { get; set; }

    public string CauseClass { get; set; }

    public string Title { get; set; }

    public string Evidence { get; set; }

    public string Fix { get; set; }
}
