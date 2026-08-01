using SysDiff.Core;
using SysDiff.Domain;

namespace SysDiff.Core.Tests;

public sealed class SeverityEngineV2Tests
{
    private readonly SeverityEngine _engine = new();

    [Fact]
    public void Evaluate_InboundAllowFirewallRule_IsHigh()
    {
        SystemArtifact artifact = Artifact(
            "firewall",
            "firewall://demo",
            ("Direction", "Inbound"),
            ("Action", "Allow"),
            ("Enabled", "True"));

        (Severity severity, _, _) = _engine.Evaluate(
            ChangeType.Added,
            before: null,
            after: artifact,
            changedProperties: []);

        Assert.Equal(Severity.High, severity);
    }

    [Fact]
    public void Evaluate_UnsignedDriver_IsCritical()
    {
        SystemArtifact artifact = Artifact(
            "drivers",
            "driver://demo",
            ("Signature", "MissingOrInvalid"),
            ("BinaryPath", @"C:\Windows\System32\drivers\demo.sys"));

        (Severity severity, _, _) = _engine.Evaluate(
            ChangeType.Added,
            before: null,
            after: artifact,
            changedProperties: []);

        Assert.Equal(Severity.Critical, severity);
    }

    [Fact]
    public void Evaluate_RootCertificate_IsHigh()
    {
        SystemArtifact artifact = Artifact(
            "certificates",
            "certificate://LocalMachine/Root/demo",
            ("StoreName", "Root"));

        (Severity severity, _, _) = _engine.Evaluate(
            ChangeType.Added,
            before: null,
            after: artifact,
            changedProperties: []);

        Assert.Equal(Severity.High, severity);
    }

    [Fact]
    public void Evaluate_InstalledApplication_IsMedium()
    {
        SystemArtifact artifact = Artifact(
            "installed-apps",
            "app://machine/x64/demo",
            ("DisplayName", "Demo"));

        (Severity severity, _, _) = _engine.Evaluate(
            ChangeType.Added,
            before: null,
            after: artifact,
            changedProperties: []);

        Assert.Equal(Severity.Medium, severity);
    }

    private static SystemArtifact Artifact(
        string provider,
        string identity,
        params (string Name, string Value)[] values) =>
        new()
        {
            ProviderId = provider,
            ArtifactType = "Test",
            Identity = identity,
            DisplayName = identity,
            Properties = values.ToDictionary(
                x => x.Name,
                x => ArtifactValue.From(x.Value),
                StringComparer.OrdinalIgnoreCase)
        };
}
