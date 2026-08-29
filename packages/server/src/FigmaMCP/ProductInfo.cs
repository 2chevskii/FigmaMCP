using System.Reflection;

namespace FigmaMCP;

internal static class ProductInfo
{
    public const string Name = "figma-mcp-server";

    public static string Version { get; } =
        typeof(ProductInfo)
            .Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "1.0.0-local";
}
