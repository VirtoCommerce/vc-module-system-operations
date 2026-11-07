using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using VirtoCommerce.SystemOperations.Core.Diagnostics;

namespace VirtoCommerce.SystemOperations.Data.Diagnostics;

// Answers "is it our code or Virto's": each frame of the stack is mapped to the assembly that defines its type, and the
// assembly to the platform, a Virto module, a module of the solution or a library. The cause class is a heuristic from
// the exception type and the message; the frames are returned as the evidence.
public class ErrorExplainer(IModuleInventoryService moduleInventory) : IErrorExplainer
{
    private const int MaxFrames = 40;
    private const string High = "high";
    private const string Medium = "medium";
    private const string Low = "low";

    // The loaded assembly names, longest first; read again when more assemblies are loaded.
    private (int Count, string[] Names) _assemblyNames = (0, []);

    public virtual ErrorExplanation Explain(string exceptionType, string message, string stackTrace)
    {
        var frames = ParseFrames(stackTrace).Take(MaxFrames).Select(MapFrame).ToList();
        var firstOwned = frames.FirstOrDefault(x => x.Owner != FrameOwners.Library);

        var explanation = new ErrorExplanation
        {
            ExceptionType = exceptionType ?? ReadExceptionType(stackTrace),
            Message = message,
            Frames = frames,
            FirstOwnedFrame = firstOwned,
            OwnCodeOnStack = frames.Any(x => x.Owner == FrameOwners.OwnModule),
        };

        Classify(explanation);

        return explanation;
    }

    public static IEnumerable<string> ParseFrames(string stackTrace)
    {
        if (string.IsNullOrEmpty(stackTrace))
        {
            yield break;
        }

        foreach (var raw in stackTrace.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith("at ", StringComparison.Ordinal))
            {
                continue;
            }

            line = line[3..];
            var location = line.IndexOf(" in ", StringComparison.Ordinal);
            yield return location > 0 ? line[..location] : line;
        }
    }

    protected virtual StackFrameOwner MapFrame(string method)
    {
        var frame = new StackFrameOwner { Method = method, Owner = FrameOwners.Library };

        var assemblyName = FindAssembly(method);
        if (assemblyName == null)
        {
            return frame;
        }

        if (moduleInventory.IsPlatformAssembly(new AssemblyName(assemblyName)))
        {
            frame.Owner = FrameOwners.Platform;
            return frame;
        }

        var module = moduleInventory.FindModuleByAssembly(assemblyName);
        if (module != null)
        {
            frame.Owner = module.Origin == ModuleOrigins.Virto ? FrameOwners.VirtoModule : FrameOwners.OwnModule;
            frame.ModuleId = module.Id;
            frame.ModuleVersion = module.Version;
        }

        return frame;
    }

    // The namespace of a type usually starts with the name of its assembly, so the longest loaded assembly name that
    // prefixes the method is its assembly. "VirtoCommerce.OrdersModule.Data.Services.X.M()" -> VirtoCommerce.OrdersModule.Data
    protected virtual string FindAssembly(string method)
    {
        var name = method.Split('(')[0];

        return GetAssemblyNames().FirstOrDefault(x => name.StartsWith(x + ".", StringComparison.Ordinal));
    }

    private string[] GetAssemblyNames()
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        var cached = _assemblyNames;
        if (cached.Count == assemblies.Length)
        {
            return cached.Names;
        }

        var names = assemblies
            .Select(x => x.GetName().Name)
            .Where(x => !string.IsNullOrEmpty(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Length)
            .ToArray();
        _assemblyNames = (assemblies.Length, names);

        return names;
    }

    private static string ReadExceptionType(string stackTrace)
    {
        var first = stackTrace?.Split('\n').FirstOrDefault()?.Trim();
        var colon = first?.IndexOf(':') ?? -1;

        return colon > 0 && !first[..colon].Contains(' ') ? first[..colon] : null;
    }

    public static void Classify(ErrorExplanation explanation)
    {
        if (!ClassifyByException(explanation))
        {
            ClassifyByFrames(explanation);
        }
    }

    // The exception type and the message decide when they name a database, a network call, a setting or a resource.
    private static bool ClassifyByException(ErrorExplanation explanation)
    {
        var type = explanation.ExceptionType ?? string.Empty;
        var text = $"{type} {explanation.Message}";

        if (ContainsAny(text, "SqlException", "NpgsqlException", "MySqlException", "DbUpdateException", "DbException"))
        {
            if (ContainsAny(text, "duplicate key", "unique constraint", "FOREIGN KEY", "REFERENCE constraint", "cannot insert the value NULL", "violates", "truncated"))
            {
                Set(explanation, CauseClasses.Data, High, "The database refused the data: a constraint or a value that does not fit.",
                    "Find the record from the message and check it against the constraint.", "Check whether an import or an integration wrote it.");
            }
            else if (ContainsAny(text, "timeout", "deadlock", "connection"))
            {
                Set(explanation, CauseClasses.Infrastructure, High, "The database timed out, deadlocked or dropped the connection.",
                    "Run check_capacity for the database latency.", "Look for long-running queries and blocking sessions on the database server.");
            }
            else
            {
                Set(explanation, CauseClasses.Infrastructure, Medium, "The error comes from the database.",
                    "Run get_platform_health.", "Check check_pending_migrations: a missing migration also fails here.");
            }

            return true;
        }

        if (ContainsAny(text, "HttpRequestException", "SocketException", "WebException", "RpcException", "BrokenCircuitException"))
        {
            Set(explanation, CauseClasses.ExternalService, High, "A call to another service failed: a payment, shipping, tax, search or integration endpoint.",
                "Find the service from the message and check that it is reachable and its credentials are valid.", "Check whether the error started when the service changed.");
            return true;
        }

        if (ContainsAny(text, "OptionsValidationException", "connection string", "is not configured", "not been configured", "configuration", "No service for type", "Unable to resolve service"))
        {
            Set(explanation, CauseClasses.Configuration, Medium, "The platform is missing a setting or a registration.",
                "Run check_configuration.", "Compare the settings with an environment that works.");
            return true;
        }

        if (ContainsAny(text, "OutOfMemoryException", "InsufficientExecutionStackException"))
        {
            Set(explanation, CauseClasses.Infrastructure, High, "The instance ran out of memory or stack.",
                "Run check_capacity.", "Find the operation that allocates: an export, an import or a large search.");
            return true;
        }

        if (ContainsAny(text, "Elasticsearch", "OpenSearch", "Lucene", "SearchException"))
        {
            Set(explanation, CauseClasses.Infrastructure, Medium, "The search engine failed or its index does not match the platform.",
                "Run get_platform_health.", "Rebuild the index of the document type in the back office if the mapping changed.");
            return true;
        }

        return false;
    }

    // Otherwise the frames decide: whose code threw it, and whose code called it.
    private static void ClassifyByFrames(ErrorExplanation explanation)
    {
        var type = explanation.ExceptionType ?? string.Empty;
        var first = explanation.FirstOwnedFrame;
        if (first?.Owner == FrameOwners.OwnModule)
        {
            Set(explanation, CauseClasses.Customization, High,
                $"The error is thrown in the solution's own module {first.ModuleId} {first.ModuleVersion}.",
                $"Ask the team that maintains {first.ModuleId}.", "Check whether a recent deployment changed it (get_incident_timeline).");
        }
        else if (explanation.OwnCodeOnStack)
        {
            var own = explanation.Frames.First(x => x.Owner == FrameOwners.OwnModule);
            Set(explanation, CauseClasses.Customization, Medium,
                $"Virto's code throws, but it was called by the solution's module {own.ModuleId}, which may pass data or an override Virto's code does not expect.",
                $"Reproduce the call without {own.ModuleId}, or ask its team first.", "If it fails without it too, prepare a support case.");
        }
        else if (IsRejectedInput(type, explanation.Message))
        {
            // Virto's code refused what it was given: a request with a wrong value, or stored data it does not expect.
            Set(explanation, CauseClasses.Data, Medium,
                "Virto's code refused its input: the request or the stored data has a value it does not accept. The code works as designed unless the input is valid.",
                "Find the request by the trace id and check what the caller sent: the back office, the storefront, an import or an integration.",
                "If the input is valid, run prepare_support_case and send it to Virto.");
        }
        else if (first?.Owner is FrameOwners.VirtoModule or FrameOwners.Platform)
        {
            Set(explanation, CauseClasses.PlatformCode, Medium,
                $"The error is thrown in Virto's code ({first.ModuleId ?? "the platform"}) and no module of the solution is on the stack. Data can still trigger it.",
                "Check whether the object the request worked on has unusual data.", "If not, run prepare_support_case and send it to Virto.");
        }
        else
        {
            Set(explanation, CauseClasses.PlatformCode, Low, "No frame of the platform or a module was recognized; the stack is incomplete or the error comes from a library.",
                "Search the errors for the same trace id to find the request.", "Paste the full stack trace into explain_error.");
        }
    }

    private static bool IsRejectedInput(string exceptionType, string message)
    {
        var type = exceptionType ?? string.Empty;
        if (ContainsAny(type, "ArgumentException", "ArgumentNullException", "ArgumentOutOfRangeException", "FormatException", "ValidationException", "KeyNotFoundException"))
        {
            return true;
        }

        var text = ErrorJournal.FirstLine(message) ?? string.Empty;
        return ContainsAny(text, "Invalid ", "is required", "does not exist", "was not found", "cannot be null");
    }

    private static void Set(ErrorExplanation explanation, string causeClass, string confidence, string reason, params string[] nextChecks)
    {
        explanation.LikelyCauseClass = causeClass;
        explanation.Confidence = confidence;
        explanation.Reason = reason;
        explanation.NextChecks = nextChecks;
    }

    private static bool ContainsAny(string text, params string[] values)
    {
        return values.Any(x => text.Contains(x, StringComparison.OrdinalIgnoreCase));
    }
}
