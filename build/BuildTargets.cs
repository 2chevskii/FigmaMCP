static class BuildTargets
{
    public const string Build = ":build";
    public const string Clean = ":clean";

    public static class Tools
    {
        public const string Restore = ":tools:restore";
    }

    public static class Docs
    {
        public const string Install = ":docs:install";
        public const string Typecheck = ":docs:typecheck";
        public const string Build = ":docs:build";
    }

    public static class Server
    {
        public const string Format = ":server:format";
        public const string Restore = ":server:restore";
        public const string Build = ":server:build";
        public const string Test = ":server:test";
        public const string PublishTests = ":server:publish-tests";
        public const string Inspector = ":server:inspector";
        public const string Publish = ":server:publish";
    }

    public static class Plugin
    {
        public const string Install = ":plugin:install";
        public const string Format = ":plugin:format";
        public const string Lint = ":plugin:lint";
        public const string Test = ":plugin:test";
        public const string Build = ":plugin:build";
    }

    public static class Package
    {
        public const string NuGet = ":package:nuget";
        public const string Server = ":package:server";
        public const string Plugin = ":package:plugin";
        public const string Release = ":package:release";
    }

    public static class Release
    {
        public const string Validate = ":release:validate";
        public const string Prepare = ":release:prepare";
        public const string Download = ":release:download";
        public const string PublishNuGet = ":release:publish:nuget";
        public const string PublishGitHubPackages = ":release:publish:github-packages";
    }
}
