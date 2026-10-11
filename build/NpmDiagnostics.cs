using System.Text;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Npm;

static class NpmDiagnostics
{
    public static NpmSettings WithDiagnostics(this NpmSettings settings, ICakeContext context)
    {
        var output = new StringBuilder();
        var outputLock = new object();

        // Cake.Npm maps quiet verbosity to --silent, which also suppresses npm errors.
        if (context.Log.Verbosity == Verbosity.Quiet)
        {
            settings.LogLevel = NpmLogLevel.Error;
        }

        settings.StandardOutputAction = line => RecordOutput(line, Console.Out);
        settings.StandardErrorAction = line => RecordOutput(line, Console.Error);
        settings.HandleExitCode = exitCode =>
        {
            if (exitCode == 0)
            {
                return true;
            }

            string diagnostics;
            lock (outputLock)
            {
                diagnostics = output.ToString().TrimEnd();
            }

            var message = $"npm failed in '{settings.WorkingDirectory}' (exit code {exitCode}).";
            if (diagnostics.Length > 0)
            {
                message += Environment.NewLine + diagnostics;
            }

            throw new CakeException(exitCode, message);
        };

        return settings;

        void RecordOutput(string line, TextWriter writer)
        {
            lock (outputLock)
            {
                output.AppendLine(line);
                writer.WriteLine(line);
            }
        }
    }
}
