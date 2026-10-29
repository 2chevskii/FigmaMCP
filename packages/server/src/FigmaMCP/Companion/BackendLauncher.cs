using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using FigmaMCP.Options;

namespace FigmaMCP.Companion;

internal static partial class BackendLauncher
{
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage(
        "SingleFile",
        "IL3000",
        Justification = "Assembly.Location is used only when launched through the dotnet host, never for a single-file executable."
    )]
    public static void Launch(ServerOptions options, string bootstrap)
    {
        var executable =
            Environment.ProcessPath ?? throw new IOException("Cannot resolve the executable.");
        var args = new List<string>();
        if (
            Path.GetFileNameWithoutExtension(executable)
                .Equals("dotnet", StringComparison.OrdinalIgnoreCase)
        )
            args.Add(typeof(BackendLauncher).Assembly.Location);
        args.AddRange([
            "--backend",
            "--port",
            options.Port.ToString(),
            "--idle-timeout-seconds",
            options.IdleTimeoutSeconds.ToString(),
            "--bootstrap-pipe",
            bootstrap,
        ]);
        if (OperatingSystem.IsWindows())
        {
            LaunchWindows(executable, args);
            return;
        }
        var start = new ProcessStartInfo("/bin/sh") { UseShellExecute = false };
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add("\"$@\" </dev/null >/dev/null 2>/dev/null &");
        start.ArgumentList.Add("figma-mcp-launcher");
        start.ArgumentList.Add(executable);
        foreach (var arg in args)
            start.ArgumentList.Add(arg);
        using var launcher =
            Process.Start(start) ?? throw new IOException("Unable to start backend launcher.");
        if (!launcher.WaitForExit(5000) || launcher.ExitCode != 0)
            throw new IOException("Backend launcher failed.");
    }

    [SupportedOSPlatform("windows")]
    private static void LaunchWindows(string executable, List<string> arguments)
    {
        // Use SWbemProperty.Value: dynamic object-specific property DISPIDs are not stable across launches.
        // WMI creates the process outside the MCP client's process tree/job.
        dynamic locator = Activator.CreateInstance(
            Type.GetTypeFromProgID("WbemScripting.SWbemLocator")!
        )!;
        dynamic services = locator.ConnectServer(".", "root\\cimv2");
        dynamic startupClass = services.Get("Win32_ProcessStartup");
        dynamic startup = startupClass.SpawnInstance_();
        startup.Properties_.Item("ShowWindow").Value = (ushort)0;
        startup.Properties_.Item("CreateFlags").Value = (uint)0x01000008; // BREAKAWAY_FROM_JOB | DETACHED_PROCESS
        startup.Properties_.Item("EnvironmentVariables").Value = Environment
            .GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .Select(e => e.Key + "=" + e.Value)
            .ToArray();
        dynamic processClass = services.Get("Win32_Process");
        dynamic input = processClass.Methods_.Item("Create").InParameters.SpawnInstance_();
        input.Properties_.Item("CommandLine").Value = string.Join(
            " ",
            new[] { executable }.Concat(arguments).Select(QuoteWindows)
        );
        input.Properties_.Item("CurrentDirectory").Value = AppContext.BaseDirectory;
        input.Properties_.Item("ProcessStartupInformation").Value = startup;
        dynamic result = processClass.ExecMethod_("Create", input);
        if ((uint)result.Properties_.Item("ReturnValue").Value != 0)
            throw new IOException(
                "Detached backend launch failed (WMI "
                    + result.Properties_.Item("ReturnValue").Value
                    + ")."
            );
    }

    private static string QuoteWindows(string value) =>
        "\""
        + System.Text.RegularExpressions.Regex.Replace(
            value,
            "(\\\\*)(\"|$)",
            m =>
                new string('\\', m.Groups[1].Length * 2) + (m.Groups[2].Value == "\"" ? "\\\"" : "")
        )
        + "\"";

    public static void DetachUnix()
    {
        if (!OperatingSystem.IsWindows() && SetSid() < 0)
            throw new IOException(
                "Cannot detach backend session: " + Marshal.GetLastPInvokeError()
            );
    }

    [LibraryImport("libc", EntryPoint = "setsid", SetLastError = true)]
    private static partial int SetSid();
}
