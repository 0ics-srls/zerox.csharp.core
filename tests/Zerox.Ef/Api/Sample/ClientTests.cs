using System.Diagnostics;
using Zerox.Ef.Api.Tests.Sample.Api;

namespace Zerox.Ef.Api.Tests.Sample;

// The TypeScript client of the example application (client/): generated from openapi/Sample.json,
// type-checked and linted, and run against the Sample on a real port. It needs Node (22.18 or later) and the npm
// registry, so it is an integration test.
[Trait("Category", "Integration")]
public sealed class ClientTests : IAsyncLifetime
{
    private SampleApp? _app;

    private SampleApp App => _app ?? throw new InvalidOperationException("InitializeAsync has not run.");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string ClientDirectory
    {
        get
        {
            var openApi = Path.GetDirectoryName(OpenApiDocumentTests.DocumentFile)
                ?? throw new InvalidOperationException("The OpenAPI document is a file in a folder.");
            var sample = Path.GetDirectoryName(openApi)
                ?? throw new InvalidOperationException("The OpenAPI folder is in the sample's folder.");
            return Path.Combine(sample, "client");
        }
    }

    public async ValueTask InitializeAsync()
    {
        _app = await SampleApp.StartAsync(onARealPort: true);
        await App.SeedAsync();
    }

    public async ValueTask DisposeAsync() => await App.DisposeAsync();

    [Fact]
    public async Task Should_PassTheTypeScriptClient_AgainstTheRunningSample()
    {
        await NpmAsync("ci --no-audit --no-fund");
        await NpmAsync("run generate");
        await NpmAsync("run check");
        await NpmAsync("test");
    }

    private async Task NpmAsync(string arguments)
    {
        var start = new ProcessStartInfo(OperatingSystem.IsWindows() ? "npm.cmd" : "npm", arguments)
        {
            WorkingDirectory = ClientDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        // The origin without the trailing slash: the generated client appends the absolute paths of the document.
        start.Environment["SAMPLE_URL"] = App.Address.GetLeftPart(UriPartial.Authority);
        using var npm = Process.Start(start) ?? throw new InvalidOperationException("npm did not start.");
        var output = npm.StandardOutput.ReadToEndAsync(Ct);
        var error = npm.StandardError.ReadToEndAsync(Ct);
        await npm.WaitForExitAsync(Ct);

        npm.ExitCode.Should().Be(0, $"npm {arguments} failed:\n{await output}\n{await error}");
    }
}
