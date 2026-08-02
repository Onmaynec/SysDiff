using System.Text.Json;
using System.Text.Json.Nodes;
using SysDiff.Storage;

namespace SysDiff.Core.Tests;

public sealed class SchemaContractServiceTests
{
    [Theory]
    [InlineData(SchemaContractKind.Snapshot, "snapshot.valid.json")]
    [InlineData(SchemaContractKind.ComparisonReport, "comparison-report.valid.json")]
    [InlineData(
        SchemaContractKind.InvestigationBundleManifest,
        "investigation-bundle-manifest.valid.json")]
    public async Task GoldenFixture_IsValid(
        SchemaContractKind kind,
        string fixtureName)
    {
        var service = new SchemaContractService();

        SchemaValidationResult result = await service.ValidateFileAsync(
            kind,
            FixturePath(fixtureName),
            CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal(SchemaValidationStatus.Valid, result.Status);
        Assert.Equal(1, result.DocumentSchemaVersion);
        Assert.Empty(result.Issues);
        Assert.Contains(result.Warnings, value =>
            value.Contains("additive", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void EmbeddedSchemas_AreDraft202012StableContracts()
    {
        var service = new SchemaContractService();

        foreach (SchemaContractDescriptor contract in service.ListContracts())
        {
            string schema = service.GetSchemaJson(contract.Kind);
            using JsonDocument document = JsonDocument.Parse(schema);
            JsonElement root = document.RootElement;

            Assert.Equal(
                "https://json-schema.org/draft/2020-12/schema",
                root.GetProperty("$schema").GetString());
            Assert.Equal(contract.SchemaId, root.GetProperty("$id").GetString());
            Assert.True(root.GetProperty("additionalProperties").GetBoolean());
            Assert.Equal(
                "stable",
                root.GetProperty("x-sysdiff-contract")
                    .GetProperty("stability")
                    .GetString());
            Assert.Equal(
                1,
                root.GetProperty("x-sysdiff-contract")
                    .GetProperty("version")
                    .GetInt32());
        }
    }

    [Fact]
    public void MissingRequiredField_IsRejected()
    {
        var service = new SchemaContractService();
        JsonObject root = ReadFixture("snapshot.valid.json").AsObject();
        Assert.True(root.Remove("Name"));

        SchemaValidationResult result = service.ValidateJson(
            SchemaContractKind.Snapshot,
            root.ToJsonString());

        Assert.False(result.IsValid);
        Assert.Equal(SchemaValidationStatus.Invalid, result.Status);
        Assert.Contains(result.Issues, value =>
            value.Path == "$.Name" && value.Code == "required");
    }

    [Fact]
    public void UnknownAdditiveFields_AreAccepted()
    {
        var service = new SchemaContractService();
        JsonObject root = ReadFixture("snapshot.valid.json").AsObject();
        root["AnotherFutureField"] = new JsonObject
        {
            ["nested"] = true
        };

        SchemaValidationResult result = service.ValidateJson(
            SchemaContractKind.Snapshot,
            root.ToJsonString());

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void SemVerPrereleaseAndBuildMetadata_AreAccepted()
    {
        var service = new SchemaContractService();
        JsonObject root = ReadFixture("snapshot.valid.json").AsObject();
        root["SysDiffVersion"] = "0.10.0-rc.1+build.5";

        SchemaValidationResult result = service.ValidateJson(
            SchemaContractKind.Snapshot,
            root.ToJsonString());

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Issues, value => value.Code == "semver");
    }

    [Fact]
    public void FutureSchemaVersion_RequiresNewerSysDiff()
    {
        var service = new SchemaContractService();
        JsonObject root = ReadFixture("snapshot.valid.json").AsObject();
        root["SchemaVersion"] = 2;

        SchemaValidationResult result = service.ValidateJson(
            SchemaContractKind.Snapshot,
            root.ToJsonString());

        Assert.False(result.IsValid);
        Assert.Equal(SchemaValidationStatus.RequiresNewerSysDiff, result.Status);
        Assert.Contains(result.Issues, value => value.Code == "requires_newer_sysdiff");
    }

    [Fact]
    public void InvalidEnum_IsRejectedWithJsonPath()
    {
        var service = new SchemaContractService();
        JsonObject root = ReadFixture("comparison-report.valid.json").AsObject();
        root["comparison"]!["noiseMode"] = "Aggressive";

        SchemaValidationResult result = service.ValidateJson(
            SchemaContractKind.ComparisonReport,
            root.ToJsonString());

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues, value =>
            value.Path == "$.comparison.noiseMode" && value.Code == "enum");
    }

    private static JsonNode ReadFixture(string name) =>
        JsonNode.Parse(File.ReadAllText(FixturePath(name)))
        ?? throw new InvalidDataException($"Fixture не содержит JSON: {name}");

    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "v1", name);
}
