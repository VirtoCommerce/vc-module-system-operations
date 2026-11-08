using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using VirtoCommerce.SystemOperations.Core.Diagnostics;
using VirtoCommerce.SystemOperations.MCP.Tools;
using Xunit;
using McpSerialization = VirtoCommerce.MCP.Core.McpSerialization;
using IMcpPageSizeResolver = VirtoCommerce.MCP.Core.Services.IMcpPageSizeResolver;

namespace VirtoCommerce.SystemOperations.Tests.Mcp;

// list_modules returns one page; an installation has more modules than a page holds, so skip reads the rest.
[Trait("Category", "Unit")]
public class ListModulesPagingTests
{
    private const int MaxPageSize = 50;

    private readonly OrientationTools _tools = new(null, null, new ModuleInventory(91), null, TimeProvider.System);

    [Fact]
    public async Task ListModules_SkipReadsThePagesAfterTheFirst()
    {
        var pages = new List<string>();
        for (var skip = 0; skip < 91; skip += MaxPageSize)
        {
            var (total, ids) = await ListAsync(skip, MaxPageSize);
            Assert.Equal(91, total);
            pages.AddRange(ids);
        }

        Assert.Equal(Enumerable.Range(0, 91).Select(ModuleInventory.Id), pages);
    }

    [Fact]
    public async Task ListModules_TakeAboveTheCap_ReturnsOneFullPage()
    {
        var (total, ids) = await ListAsync(0, 200);

        Assert.Equal(91, total);
        Assert.Equal(MaxPageSize, ids.Count);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(90, 90)]
    public async Task ListModules_SkipOutsideTheList_IsClamped(int skip, int firstIndex)
    {
        var (_, ids) = await ListAsync(skip, 10);

        Assert.Equal(ModuleInventory.Id(firstIndex), ids[0]);
    }

    [Fact]
    public async Task ListModules_SkipPastTheEnd_ReturnsNoRows()
    {
        var (total, ids) = await ListAsync(500, 10);

        Assert.Equal(91, total);
        Assert.Empty(ids);
    }

    [Fact]
    public void ListModules_DeclaresSkipBeforeTake()
    {
        var names = typeof(OrientationTools).GetMethod(nameof(OrientationTools.ListModules))!
            .GetParameters().Select(x => x.Name).ToArray();

        Assert.Equal(["pageSizeResolver", "filter", "skip", "take"], names);
    }

    private async Task<(int Total, List<string> Ids)> ListAsync(int skip, int? take)
    {
        var result = await _tools.ListModules(new PageSizeResolver(), ModuleFilters.All, skip, take);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result, McpSerialization.Options));

        var total = json.RootElement.GetProperty("total_count").GetInt32();
        var ids = json.RootElement.GetProperty("modules").EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToList();
        return (total, ids);
    }

    private sealed class PageSizeResolver : IMcpPageSizeResolver
    {
        public Task<int> GetPageSizeAsync(int? take) => Task.FromResult(Math.Clamp(take ?? 20, 1, MaxPageSize));
    }

    private sealed class ModuleInventory(int count) : IModuleInventoryService
    {
        public static string Id(int index) => $"VirtoCommerce.Module{index:D3}";

        public IList<ModuleDescription> GetModules(string filter = ModuleFilters.All) =>
            Enumerable.Range(0, count).Select(x => new ModuleDescription { Id = Id(x), Version = "3.1000.0", Origin = "virto" }).ToList();

        public ModuleDescription FindModuleByAssembly(string assemblyName) => null;

        public bool IsPlatformAssembly(AssemblyName assemblyName) => false;
    }
}
