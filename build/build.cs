#!/usr/bin/env dotnet

#:sdk Cake.Sdk@6.2.0
#:package Cake.Npm@5.1.0
#:package Octokit@14.0.0
#:property IncludeAdditionalFiles=./*.cs

var rootDirectory = Context.Environment.WorkingDirectory;
var paths = BuildPaths.Create(rootDirectory);

Task(BuildTargets.Clean).Does(() => CoreTasks.Clean(Context, paths));
Task(BuildTargets.Tools.Restore).Does(() => CoreTasks.RestoreTools(Context));
Task(BuildTargets.Version.Calculate).Does(() => VersionTasks.Print(Context, paths));

Task(BuildTargets.Commits.Install).Does(() => CommitTasks.Install(Context, paths));
Task(BuildTargets.Commits.Check)
    .IsDependentOn(BuildTargets.Commits.Install)
    .Does(() => CommitTasks.Check(Context, paths));
Task(BuildTargets.Commits.InstallHook)
    .IsDependentOn(BuildTargets.Commits.Install)
    .Does(() => CommitTasks.InstallHook(Context, paths));

Task(BuildTargets.Docs.Install).Does(() => DocsTasks.Install(Context, paths));
Task(BuildTargets.Docs.Typecheck)
    .IsDependentOn(BuildTargets.Docs.Install)
    .Does(() => DocsTasks.Typecheck(Context, paths));
Task(BuildTargets.Docs.Build)
    .IsDependentOn(BuildTargets.Docs.Install)
    .IsDependentOn(BuildTargets.Docs.Typecheck)
    .Does(() => DocsTasks.Build(Context, paths));

Task(BuildTargets.Server.Format)
    .IsDependentOn(BuildTargets.Tools.Restore)
    .Does(() => ServerTasks.Format(Context, paths));
Task(BuildTargets.Server.Restore).Does(() => ServerTasks.Restore(Context, paths));
Task(BuildTargets.Server.Build)
    .IsDependentOn(BuildTargets.Server.Restore)
    .Does(() => ServerTasks.Build(Context, paths));
Task(BuildTargets.Server.Test).Does(() => ServerTasks.Test(Context, paths));
Task(BuildTargets.Server.PublishTests)
    .IsDependentOn(BuildTargets.Server.Restore)
    .Does(() => ServerTasks.PublishTests(Context, paths));
Task(BuildTargets.Server.Inspector)
    .IsDependentOn(BuildTargets.Server.Build)
    .Does(() => ServerTasks.Inspect(Context, paths));
Task(BuildTargets.Server.Publish)
    .IsDependentOn(BuildTargets.Server.Restore)
    .Does(() => ServerTasks.Publish(Context, paths));

Task(BuildTargets.Plugin.Install).Does(() => PluginTasks.Install(Context, paths));
Task(BuildTargets.Plugin.Format)
    .IsDependentOn(BuildTargets.Plugin.Install)
    .Does(() => PluginTasks.Format(Context, paths));
Task(BuildTargets.Plugin.Lint)
    .IsDependentOn(BuildTargets.Plugin.Install)
    .Does(() => PluginTasks.Lint(Context, paths));
Task(BuildTargets.Plugin.Test)
    .IsDependentOn(BuildTargets.Plugin.Install)
    .Does(() => PluginTasks.Test(Context, paths));
Task(BuildTargets.Plugin.Build)
    .IsDependentOn(BuildTargets.Plugin.Install)
    .Does(() => PluginTasks.Build(Context, paths));

Task(BuildTargets.Build)
    .IsDependentOn(BuildTargets.Docs.Build)
    .IsDependentOn(BuildTargets.Server.Build)
    .IsDependentOn(BuildTargets.Plugin.Build);

Task(BuildTargets.Package.NuGet)
    .IsDependentOn(BuildTargets.Server.Restore)
    .Does(() => PackageTasks.CreateNuGetPackage(Context, paths));
Task(BuildTargets.Package.Server)
    .IsDependentOn(BuildTargets.Server.Publish)
    .Does(() => PackageTasks.CreateServerArchive(Context, paths));
Task(BuildTargets.Package.Plugin)
    .IsDependentOn(BuildTargets.Plugin.Build)
    .Does(() => PackageTasks.CreatePluginArchive(Context, paths));
Task(BuildTargets.Package.Release)
    .IsDependentOn(BuildTargets.Package.NuGet)
    .IsDependentOn(BuildTargets.Package.Server)
    .IsDependentOn(BuildTargets.Package.Plugin);

Task(BuildTargets.Release.Stage)
    .IsDependentOn(BuildTargets.Commits.Install)
    .Does(() => ReleaseTasks.Stage(Context, paths));
Task(BuildTargets.Release.Build)
    .IsDependentOn(BuildTargets.Release.Stage)
    .IsDependentOn(BuildTargets.Package.Release);
Task(BuildTargets.Release.Validate).Does(() => ReleaseTasks.Validate(Context, paths));
Task(BuildTargets.Release.Prepare)
    .IsDependentOn(BuildTargets.Release.Build)
    .Does(async () => await ReleaseTasks.CreateOrUpdateDraft(Context, paths));
Task(BuildTargets.Release.Download)
    .IsDependentOn(BuildTargets.Release.Validate)
    .Does(async () => await ReleaseTasks.DownloadNuGetPackage(Context, paths));
Task(BuildTargets.Release.PublishNuGet)
    .IsDependentOn(BuildTargets.Release.Download)
    .Does(() => ReleaseTasks.PublishToNuGet(Context, paths));
Task(BuildTargets.Release.PublishGitHubPackages)
    .IsDependentOn(BuildTargets.Release.Download)
    .Does(() => ReleaseTasks.PublishToGitHubPackages(Context, paths));

RunTarget(Argument<string>("target", BuildTargets.Build));
