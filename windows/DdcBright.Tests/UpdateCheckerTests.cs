using System.Net;
using System.Net.Http;

namespace DdcBright.Tests;

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("v1.2.3+abcdef", "1.2.3")]
    public void ParseVersion_StripsLeadingVAndBuildMetadata(string raw, string expected)
    {
        Assert.Equal(Version.Parse(expected), UpdateChecker.ParseVersion(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-version")]
    public void ParseVersion_ReturnsNull_ForUnparseableInput(string? raw)
    {
        Assert.Null(UpdateChecker.ParseVersion(raw));
    }

    [Fact]
    public async Task CheckAsync_ReturnsUpdateAvailable_WhenLatestTagIsNewer()
    {
        using var client = new HttpClient(new StubHandler("""{"tag_name":"v2.0.0"}"""));

        var result = await UpdateChecker.CheckAsync(client, "1.0.0");

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal("2.0.0", result.LatestVersion);
    }

    [Fact]
    public async Task CheckAsync_ReturnsUpToDate_WhenLatestTagMatchesCurrent()
    {
        using var client = new HttpClient(new StubHandler("""{"tag_name":"v1.0.0"}"""));

        var result = await UpdateChecker.CheckAsync(client, "1.0.0");

        Assert.Equal(UpdateCheckStatus.UpToDate, result.Status);
    }

    [Fact]
    public async Task CheckAsync_ReturnsUnknown_WhenCurrentVersionIsUnparseable()
    {
        // Dev builds without -p:Version= set fall back to a non-semver info version.
        using var client = new HttpClient(new StubHandler("""{"tag_name":"v1.0.0"}"""));

        var result = await UpdateChecker.CheckAsync(client, currentVersion: null);

        Assert.Equal(UpdateCheckStatus.Unknown, result.Status);
    }

    [Fact]
    public async Task CheckAsync_ReturnsUnknown_OnHttpFailure()
    {
        using var client = new HttpClient(new StubHandler(json: "", statusCode: HttpStatusCode.ServiceUnavailable));

        var result = await UpdateChecker.CheckAsync(client, "1.0.0");

        Assert.Equal(UpdateCheckStatus.Unknown, result.Status);
    }

    private sealed class StubHandler(string json, HttpStatusCode statusCode = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent(json) });
    }
}
