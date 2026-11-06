using FigmaMCP.Options;

namespace FigmaMCP.Tests.Unit.Options;

public sealed class ServerOptionsTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("86401")]
    [InlineData("abc")]
    public void RejectsInvalidIdleTimeout(string value)
    {
        Assert.False(ServerOptions.TryParse(["--idle-timeout-seconds", value], out _, out _));
    }

    [Fact]
    public void AcceptsCombinedOptionsAndRejectsDuplicates()
    {
        Assert.True(
            ServerOptions.TryParse(
                ["--idle-timeout-seconds", "120", "--port", "4000"],
                out var options,
                out _
            )
        );
        Assert.Equal(120, options!.IdleTimeoutSeconds);
        Assert.Equal(4000, options.Port);
        Assert.False(ServerOptions.TryParse(["--port", "4000", "--port", "4001"], out _, out _));
    }

    [Fact]
    public void TryParseUsesTheDefaultPortWhenNoArgumentsAreSupplied()
    {
        var success = ServerOptions.TryParse([], out var options, out var error);

        Assert.True(success);
        Assert.Equal(ServerOptions.DefaultPort, options?.Port);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("65535", 65535)]
    public void TryParseAcceptsPortsInRange(string value, int expectedPort)
    {
        var success = ServerOptions.TryParse(["--port", value], out var options, out var error);

        Assert.True(success);
        Assert.Equal(expectedPort, options?.Port);
        Assert.Null(error);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("65536")]
    [InlineData("not-a-number")]
    public void TryParseRejectsInvalidPorts(string value)
    {
        var success = ServerOptions.TryParse(["--port", value], out var options, out var error);

        Assert.False(success);
        Assert.Null(options);
        Assert.NotNull(error);
    }
}
