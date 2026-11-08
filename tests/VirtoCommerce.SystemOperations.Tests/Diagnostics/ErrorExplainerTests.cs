using System;
using System.Collections.Generic;
using System.Reflection;
using VirtoCommerce.SystemOperations.Core.Diagnostics;
using VirtoCommerce.SystemOperations.Data.Diagnostics;
using Xunit;

namespace VirtoCommerce.SystemOperations.Tests.Diagnostics;

// The frames use namespaces of assemblies loaded in the test run: VirtoCommerce.SystemOperations.Data stands for a
// Virto module, VirtoCommerce.SystemOperations.MCP for a module of the solution, xunit.v3.core for a library.
[Trait("Category", "Unit")]
public class ErrorExplainerTests
{
    private const string VirtoFrame = "   at VirtoCommerce.SystemOperations.Data.Diagnostics.OrderService.SaveAsync(Order order) in C:\\src\\OrderService.cs:line 42";
    private const string OwnFrame = "   at VirtoCommerce.SystemOperations.MCP.Tools.CustomOrderHandler.Handle(Order order)";
    private const string PlatformFrame = "   at VirtoCommerce.Platform.Core.Common.Something.Run()";
    private const string LibraryFrame = "   at Xunit.Assert.Equal(Object expected, Object actual)";

    private readonly ErrorExplainer _explainer = new(new StubModuleInventory());

    public ErrorExplainerTests()
    {
        // The assemblies of the frames are loaded in a running platform; in the test run they are loaded on first use, and
        // which other tests ran before must not decide the result.
        _ = typeof(VirtoCommerce.Platform.Core.Common.PlatformVersion).Assembly;
        _ = typeof(ErrorExplainer).Assembly;
        _ = typeof(VirtoCommerce.SystemOperations.MCP.McpAssemblyMarker).Assembly;
    }

    [Fact]
    public void Explain_BlamesTheSolution_When_ItsModuleThrows()
    {
        var explanation = _explainer.Explain("System.NullReferenceException", "Object reference not set", Stack(LibraryFrame, OwnFrame, VirtoFrame));

        Assert.Equal(CauseClasses.Customization, explanation.LikelyCauseClass);
        Assert.Equal(FrameOwners.OwnModule, explanation.FirstOwnedFrame.Owner);
        Assert.Equal("Contoso.Orders", explanation.FirstOwnedFrame.ModuleId);
        Assert.True(explanation.OwnCodeOnStack);
        Assert.Equal(FrameOwners.Library, explanation.Frames[0].Owner);
    }

    [Fact]
    public void Explain_PointsAtTheSolution_When_ItCallsVirtoCodeThatThrows()
    {
        var explanation = _explainer.Explain("System.ArgumentException", "Value cannot be empty", Stack(VirtoFrame, OwnFrame));

        Assert.Equal(CauseClasses.Customization, explanation.LikelyCauseClass);
        Assert.Equal(FrameOwners.VirtoModule, explanation.FirstOwnedFrame.Owner);
        Assert.Contains("Contoso.Orders", explanation.Reason);
    }

    [Fact]
    public void Explain_BlamesVirtoCode_When_NoModuleOfTheSolutionIsOnTheStack()
    {
        var explanation = _explainer.Explain("System.InvalidOperationException", "Sequence contains no elements", Stack(VirtoFrame, PlatformFrame));

        Assert.Equal(CauseClasses.PlatformCode, explanation.LikelyCauseClass);
        Assert.False(explanation.OwnCodeOnStack);
        Assert.Equal(FrameOwners.Platform, explanation.Frames[1].Owner);
        Assert.Contains(explanation.NextChecks, x => x.Contains("prepare_support_case"));
    }

    // Virto's code that refuses a wrong value works as designed: the input is the problem, not the code.
    [Theory]
    [InlineData("VirtoCommerce.Platform.Core.Exceptions.PlatformException", "Invalid path /assets/no-such-backup.zip")]
    [InlineData("System.ArgumentNullException", "Value cannot be null. (Parameter 'key')")]
    public void Explain_BlamesTheInput_When_VirtoCodeRefusesIt(string exceptionType, string message)
    {
        var explanation = _explainer.Explain(exceptionType, message, Stack(VirtoFrame, PlatformFrame));

        Assert.Equal(CauseClasses.Data, explanation.LikelyCauseClass);
        Assert.Equal("medium", explanation.Confidence);
        Assert.Contains(explanation.NextChecks, x => x.Contains("trace id"));
    }

    [Theory]
    [InlineData("Microsoft.Data.SqlClient.SqlException", "Execution Timeout Expired", CauseClasses.Infrastructure)]
    [InlineData("Microsoft.EntityFrameworkCore.DbUpdateException", "Cannot insert duplicate key row in object 'dbo.Product'", CauseClasses.Data)]
    [InlineData("System.Net.Http.HttpRequestException", "Connection refused (payments.example.com:443)", CauseClasses.ExternalService)]
    [InlineData("Microsoft.Extensions.Options.OptionsValidationException", "Search:Provider is required", CauseClasses.Configuration)]
    [InlineData("System.OutOfMemoryException", "Insufficient memory", CauseClasses.Infrastructure)]
    public void Explain_ClassifiesByTheExceptionAndTheMessage(string exceptionType, string message, string expected)
    {
        var explanation = _explainer.Explain(exceptionType, message, Stack(OwnFrame));

        Assert.Equal(expected, explanation.LikelyCauseClass);
    }

    [Fact]
    public void Explain_ReadsTheExceptionTypeFromAPastedStackTrace()
    {
        var explanation = _explainer.Explain(null, "boom", "System.TimeoutException: boom\n" + VirtoFrame);

        Assert.Equal("System.TimeoutException", explanation.ExceptionType);
        Assert.Single(explanation.Frames);
    }

    private static string Stack(params string[] frames)
    {
        return string.Join("\n", frames);
    }

    private sealed class StubModuleInventory : IModuleInventoryService
    {
        public IList<ModuleDescription> GetModules(string filter = ModuleFilters.All)
        {
            return [];
        }

        public ModuleDescription FindModuleByAssembly(string assemblyName)
        {
            return assemblyName switch
            {
                "VirtoCommerce.SystemOperations.Data" => new ModuleDescription { Id = "VirtoCommerce.Orders", Version = "3.1019.0", Origin = ModuleOrigins.Virto },
                "VirtoCommerce.SystemOperations.MCP" => new ModuleDescription { Id = "Contoso.Orders", Version = "1.2.0", Origin = ModuleOrigins.Own },
                _ => null,
            };
        }

        public bool IsPlatformAssembly(AssemblyName assemblyName)
        {
            return assemblyName.Name.StartsWith("VirtoCommerce.Platform.", StringComparison.Ordinal);
        }
    }
}
