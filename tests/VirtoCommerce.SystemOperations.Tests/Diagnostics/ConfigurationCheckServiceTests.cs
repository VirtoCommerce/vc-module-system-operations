using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using VirtoCommerce.SystemOperations.Core.Diagnostics;
using VirtoCommerce.SystemOperations.Data.Diagnostics;
using Xunit;

namespace VirtoCommerce.SystemOperations.Tests.Diagnostics;

[Trait("Category", "Unit")]
public class ConfigurationCheckServiceTests
{
    [Fact]
    public async Task CheckAsync_RaisesSharedStorageToCritical_When_SeveralInstancesRun()
    {
        // Arrange
        var service = CreateService(new Dictionary<string, string>(), Profile(instances: 2, assets: "FileSystem", search: "Lucene", redis: false));

        // Act
        var findings = await service.CheckAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(FindingSeverities.Critical, Find(findings, "redis-missing").Severity);
        Assert.Equal(FindingSeverities.Critical, Find(findings, "assets-local-file-system").Severity);
        Assert.Equal(FindingSeverities.Critical, Find(findings, "search-lucene").Severity);
        Assert.NotNull(Find(findings, "push-notifications-single-instance"));
        Assert.Equal(FindingSeverities.Critical, findings[0].Severity);
    }

    [Fact]
    public async Task CheckAsync_OnlyAdvises_When_OneInstanceRuns()
    {
        // Arrange
        var service = CreateService(new Dictionary<string, string>(), Profile(instances: 1, assets: "FileSystem", search: "ElasticSearch8", redis: false));

        // Act
        var findings = await service.CheckAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(FindingSeverities.Info, Find(findings, "redis-missing").Severity);
        Assert.Equal(FindingSeverities.Info, Find(findings, "assets-local-file-system").Severity);
        Assert.Null(Find(findings, "search-lucene"));
        Assert.Null(Find(findings, "push-notifications-single-instance"));
    }

    [Fact]
    public async Task CheckAsync_FindsTheDevelopmentCertificate_TheSamplePassword_AndDebugLogs_InProduction()
    {
        // Arrange
        var configuration = new Dictionary<string, string>
        {
            ["Auth:PrivateKeyPath"] = "./certificates/virtocommerce.pfx",
            ["Auth:PrivateKeyPassword"] = "virto",
            ["ConnectionStrings:VirtoCommerce"] = "Data Source=db;User ID=virto;Password=virto;",
            ["Serilog:MinimumLevel:Default"] = "Debug",
            ["VirtoCommerce:AllowInsecureHttp"] = "true",
        };
        var service = CreateService(configuration, Profile(instances: 1, redis: true));

        // Act
        var findings = await service.CheckAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(FindingSeverities.Critical, Find(findings, "signing-certificate-default").Severity);
        Assert.Equal(FindingSeverities.Critical, Find(findings, "database-default-password").Severity);
        Assert.NotNull(Find(findings, "logging-verbose"));
        Assert.NotNull(Find(findings, "insecure-http"));
        Assert.DoesNotContain(findings, x => x.Evidence.Contains("Password=virto"));
    }

    [Fact]
    public async Task CheckAsync_RelaxesTheRules_InDevelopment()
    {
        // Arrange
        var configuration = new Dictionary<string, string>
        {
            ["Auth:PrivateKeyPath"] = "./certificates/virtocommerce.pfx",
            ["Auth:PrivateKeyPassword"] = "virto",
            ["Serilog:MinimumLevel:Default"] = "Debug",
        };
        var service = CreateService(configuration, Profile(instances: 1, redis: true), Environments.Development);

        // Act
        var findings = await service.CheckAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(FindingSeverities.Warning, Find(findings, "environment-development").Severity);
        Assert.Equal(FindingSeverities.Info, Find(findings, "signing-certificate-default").Severity);
        Assert.Null(Find(findings, "logging-verbose"));
    }

    [Theory]
    [InlineData("expired", "license-expired", FindingSeverities.Critical)]
    [InlineData("expiring", "license-expiring", FindingSeverities.Warning)]
    [InlineData("missing", "license-missing", FindingSeverities.Info)]
    public async Task CheckAsync_ReportsTheLicense(string status, string id, string severity)
    {
        // Arrange
        var profile = Profile(instances: 1, redis: true);
        profile.License = new LicenseInfo { Status = status, ExpirationDate = new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc) };
        var service = CreateService(new Dictionary<string, string>(), profile);

        // Act
        var findings = await service.CheckAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(severity, Find(findings, id).Severity);
    }

    private static Finding Find(IList<Finding> findings, string id)
    {
        return findings.SingleOrDefault(x => x.Id == id);
    }

    private static EnvironmentProfile Profile(int instances, string assets = "AzureBlobStorage", string search = "ElasticSearch8", bool redis = true)
    {
        return new EnvironmentProfile
        {
            KnownInstanceCount = instances,
            AssetsProvider = assets,
            SearchProvider = search,
            RedisConfigured = redis,
            PushNotificationsScalability = "None",
            HangfireStorage = "Database",
            License = new LicenseInfo { Status = "active" },
            Modules = new ModuleCounts(),
        };
    }

    private static ConfigurationCheckService CreateService(Dictionary<string, string> values, EnvironmentProfile profile, string environment = "Production")
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        return new ConfigurationCheckService(configuration, new StubHostEnvironment(environment), new StubProfileService(profile));
    }

    private sealed class StubProfileService(EnvironmentProfile profile) : IEnvironmentProfileService
    {
        public Task<EnvironmentProfile> GetProfileAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(profile);
        }
    }

    private sealed class StubHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
