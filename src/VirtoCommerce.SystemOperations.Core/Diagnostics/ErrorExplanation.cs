using System.Collections.Generic;

namespace VirtoCommerce.SystemOperations.Core.Diagnostics;

// Where an error comes from: the frames of its stack mapped to the platform, Virto's modules or the solution's own code.
public class ErrorExplanation
{
    public string ExceptionType { get; set; }

    public string Message { get; set; }

    // The frame closest to the throw that is not the runtime or a third-party library.
    public StackFrameOwner FirstOwnedFrame { get; set; }

    // True when a frame of the solution's own modules is on the stack: the solution's code is involved.
    public bool OwnCodeOnStack { get; set; }

    // CauseClasses: the most likely class, with the reasoning in Reason.
    public string LikelyCauseClass { get; set; }

    // "high", "medium" or "low": how strongly the evidence supports the cause class.
    public string Confidence { get; set; }

    public string Reason { get; set; }

    public IList<string> NextChecks { get; set; } = [];

    public IList<StackFrameOwner> Frames { get; set; } = [];
}

public class StackFrameOwner
{
    public string Method { get; set; }

    // FrameOwners: the platform, a Virto module, a module of the solution, or a library.
    public string Owner { get; set; }

    public string ModuleId { get; set; }

    public string ModuleVersion { get; set; }
}

public static class FrameOwners
{
    public const string Platform = "platform";
    public const string VirtoModule = "virto_module";
    public const string OwnModule = "own_module";
    public const string Library = "library";
}
