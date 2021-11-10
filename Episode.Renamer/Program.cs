//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="RossKing">
// Copyright (c) RossKing. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Hosting;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;
using Episode.Renamer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using TagLib;

static char[] GetInvalidFileNameChars() => new char[]
{
    '\"', '<', '>', '|', '\0',
    (char)1, (char)2, (char)3, (char)4, (char)5, (char)6, (char)7, (char)8, (char)9, (char)10,
    (char)11, (char)12, (char)13, (char)14, (char)15, (char)16, (char)17, (char)18, (char)19, (char)20,
    (char)21, (char)22, (char)23, (char)24, (char)25, (char)26, (char)27, (char)28, (char)29, (char)30,
    (char)31, ':', '*', '?', '\\', '/',
};

static char[] GetInvalidPathChars() => new char[]
{
    '\"', '<', '>', '|', '\0',
    (char)1, (char)2, (char)3, (char)4, (char)5, (char)6, (char)7, (char)8, (char)9, (char)10,
    (char)11, (char)12, (char)13, (char)14, (char)15, (char)16, (char)17, (char)18, (char)19, (char)20,
    (char)21, (char)22, (char)23, (char)24, (char)25, (char)26, (char)27, (char)28, (char)29, (char)30,
    (char)31, ':', '*', '?',
};

return await BuildCommandLine()
    .UseHost(
        args => Host.CreateDefaultBuilder(args).UseContentRoot(System.AppDomain.CurrentDomain.BaseDirectory),
        host => host
            .UseSerilog((hostBuilderContext, loggerConfiguration) => loggerConfiguration.ReadFrom.Configuration(hostBuilderContext.Configuration))
            .ConfigureServices((builder, services) => services.Configure<InvocationLifetimeOptions>(options => options.SuppressStatusMessages = true)))
    .UseDefaults()
    .Build()
    .InvokeAsync(args.Select(arg => System.Environment.ExpandEnvironmentVariables(arg)).ToArray())
    .ConfigureAwait(false);

static CommandLineBuilder BuildCommandLine()
{
    var rootCommandBuilder = new CommandLineBuilder()
        .AddArgument(new Argument<System.IO.DirectoryInfo>("source"))
        .AddOption(new Option<System.IO.DirectoryInfo>(new[] { "--movies" }, "The destination folder for movies. If unset, defaults to \"--tv\"").ExistingOnly())
        .AddOption(new Option<System.IO.DirectoryInfo>(new[] { "--tv" }, "The destination folder for TV shows. If unset, defaults to \"--movies\"").ExistingOnly())
        .AddOption(new Option<bool>(new[] { "-m", "--move" }, "Moves the files"))
        .AddOption(new Option<bool>(new[] { "-r", "--recursive" }, "Recursively searches <SOURCE>"))
        .AddOption(new Option<bool>(new[] { "-n", "--dry-run" }, "Don’t actually move/copy any file(s). Instead, just show if they exist and would otherwise be moved/copied by the command."))
        .AddOption(new Option<bool>(new[] { "-i", "--inplace" }, "Renames the files in place, rather than to <DESTINATION>."));

    rootCommandBuilder.Command.Handler = CommandHandler.Create<IHost, System.IO.DirectoryInfo, System.IO.DirectoryInfo, System.IO.DirectoryInfo, bool, bool, bool, bool>(Process);

    return rootCommandBuilder;
}

static void Process(
    IHost host,
    System.IO.DirectoryInfo source,
    System.IO.DirectoryInfo movies,
    System.IO.DirectoryInfo tv,
    bool move = false,
    bool recursive = false,
    bool dryRun = false,
    bool inplace = false)
{
    var episodeNumberByteVector = (ReadOnlyByteVector)"tves";
    var showNameByteVector = (ReadOnlyByteVector)"tvsh";
    var seasonNumberByteVector = (ReadOnlyByteVector)"tvsn";
    var workByteVector = new ReadOnlyByteVector(new byte[] { 169, 119, 114, 107 }, 4);
    var contentIdByteVector = (ReadOnlyByteVector)"cnID";

    tv ??= movies;
    movies ??= tv;

    // search for all files in the source directory
    var programLogger = host.Services.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>().CreateLogger("Program");

    foreach (var file in source.EnumerateFiles("*.*", new System.IO.EnumerationOptions { RecurseSubdirectories = recursive, IgnoreInaccessible = true, AttributesToSkip = System.IO.FileAttributes.Hidden }))
    {
        if (file.Length == 0)
        {
            continue;
        }

        File tagLibFile = default;
        System.IO.FileInfo path = default;

        try
        {
            tagLibFile = File.Create(file.FullName);

            if (tagLibFile.GetTag(TagTypes.Apple) is TagLib.Mpeg4.AppleTag appleTag)
            {
                if (appleTag.IsMovie())
                {
                    var directory = inplace
                        ? file.DirectoryName.ReplaceAll(GetInvalidPathChars())
                        : System.IO.Path.Combine(movies.FullName, "Movies").ReplaceAll(GetInvalidPathChars());

                    var fileNameWithoutExtension = $"{appleTag.Title.Sanitise()} ({appleTag.Year})";
                    if (appleTag.TryGetString(workByteVector, out var work))
                    {
                        if (!inplace)
                        {
                            directory = System.IO.Path.Combine(directory, fileNameWithoutExtension.ReplaceAll(GetInvalidPathChars()));
                        }

                        work = work.Trim();
                        if (work.Length != 0)
                        {
                            fileNameWithoutExtension += " - ";
                            fileNameWithoutExtension += work;
                        }
                    }

                    var fileName = (fileNameWithoutExtension + file.Extension).ReplaceAll(GetInvalidFileNameChars());
                    path = new System.IO.FileInfo(System.IO.Path.Combine(directory, fileName));
                }
                else if (appleTag.IsTvShow())
                {
                    var showName = string.Join("; ", appleTag.GetText(showNameByteVector)).Sanitise();
                    var seasonNumber = appleTag.GetUInt32(seasonNumberByteVector);
                    var episodeNumber = appleTag.GetUInt32(episodeNumberByteVector);

                    var directory = inplace
                        ? file.DirectoryName.ReplaceAll(GetInvalidPathChars())
                        : System.IO.Path.Combine(tv.FullName, "TV Shows", showName, $"Season {seasonNumber:00}").ReplaceAll(GetInvalidPathChars());
                    var fileNameWithoutExtension = $"{showName} - s{seasonNumber:00}e{episodeNumber:00}";
                    if (appleTag.TryGetUInt32(contentIdByteVector, out var contentId) && contentId != default)
                    {
                        // This is part of an episode
                        fileNameWithoutExtension += " - ";
                        fileNameWithoutExtension += "part";
                        fileNameWithoutExtension += contentId;
                    }
                    else
                    {
                        // This is a single episode
                        fileNameWithoutExtension += " - ";
                        fileNameWithoutExtension += appleTag.Title.Sanitise();
                    }

                    if (appleTag.TryGetString(workByteVector, out var work))
                    {
                        fileNameWithoutExtension += " - ";
                        fileNameWithoutExtension += work;
                    }

                    var fileName = (fileNameWithoutExtension + file.Extension).ReplaceAll(GetInvalidFileNameChars());
                    path = new System.IO.FileInfo(System.IO.Path.Combine(directory, fileName));
                }
                else
                {
                    programLogger.LogInformation("Failed to match {File} to either Movie or TV Show", file.Name);
                }
            }
            else
            {
                programLogger.LogDebug("Found non 'Apple' format at {File}", file.Name);
            }

            tagLibFile.Mode = File.AccessMode.Closed;
        }
        catch (UnsupportedFormatException)
        {
            programLogger.LogDebug("Unsupported file - {File}", file.Name);
        }
        catch (CorruptFileException)
        {
            programLogger.LogDebug("Corrupt file - {File}", file.Name);
        }
        finally
        {
            tagLibFile?.Dispose();
        }

        if (path != null)
        {
            if (path.Exists)
            {
                // see if this is the same size
                if (path.Length == file.Length)
                {
                    programLogger.LogDebug("{Source} has the same length as {Destination}", file.FullName, path.FullName);
                    continue;
                }
            }

            if (!dryRun)
            {
                path.Directory.Create();
            }

            if (move)
            {
                if (!path.Exists || inplace)
                {
                    programLogger.LogInformation("Moving {Source} to {Destination}", file.FullName, path.FullName);
                    if (!dryRun)
                    {
                        file.MoveTo(path.FullName);
                    }
                }
                else
                {
                    programLogger.LogInformation("Replacing {Destination} with {Source} with a move", path.FullName, file.FullName);
                    if (!dryRun)
                    {
                        file.CopyTo(path.FullName, true);
                        if (file.Exists)
                        {
                            file.Delete();
                        }
                    }
                }
            }
            else
            {
                if (path.Exists)
                {
                    programLogger.LogInformation("Replacing {Destination} with {Source} by a copy", path.FullName, file.FullName);
                }
                else
                {
                    programLogger.LogInformation("Coping {Source} to {Destination}", file.FullName, path.FullName);
                }

                if (!dryRun)
                {
                    file.CopyTo(path.FullName, true);
                }
            }
        }
    }
}