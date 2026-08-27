using FigmaMCP.Options;

namespace FigmaMCP.Tests.Unit.Options;

public sealed class ServerOptionsTests
{
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
