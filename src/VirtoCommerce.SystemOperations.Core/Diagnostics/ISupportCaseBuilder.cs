using System.Threading;
using System.Threading.Tasks;

namespace VirtoCommerce.SystemOperations.Core.Diagnostics;

// A problem report for Virto support in Markdown, with secrets and personal data removed. A person reviews and sends it.
public interface ISupportCaseBuilder
{
    Task<string> BuildAsync(SupportCaseRequest request, CancellationToken cancellationToken = default);
}

public class SupportCaseRequest
{
    public string Symptom { get; set; }

    public string StepsToReproduce { get; set; }

    public string WhatWasTried { get; set; }

    // The window of errors, failed jobs and instance starts to include.
    public int Hours { get; set; } = 24;

    // The error the case is about; the most frequent errors of the window when empty.
    public string Fingerprint { get; set; }
}
