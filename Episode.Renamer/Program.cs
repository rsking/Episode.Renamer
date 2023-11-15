//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="RossKing">
// Copyright (c) RossKing. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System.CommandLine;
using Episode.Renamer;
using Microsoft.Extensions.DependencyInjection;
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
    .InvokeAsync(args.Select(Environment.ExpandEnvironmentVariables).ToArray())
    .ConfigureAwait(false);

static CliConfiguration BuildCommandLine()
{
    var sourceArgument = new CliArgument<DirectoryInfo>("source");
    var moviesOption = new CliOption<DirectoryInfo>("--movies") { Description = "The destination folder for movies. If unset, defaults to \"--tv\"" }.AcceptExistingOnly();
    var tvOption = new CliOption<DirectoryInfo>("--tv") { Description = "The destination folder for TV shows. If unset, defaults to \"--movies\"" }.AcceptExistingOnly();
    var moveOption = new CliOption<bool>("-m", "--move") { Description = "Moves the files" };
    var recursiveOption = new CliOption<bool>("-r", "--recursive") { Description = "Recursively searches <SOURCE>" };
    var dryRunOption = new CliOption<bool>("-n", "--dry-run") { Description = "Don't actually move/copy any file(s). Instead, just show if they exist and would otherwise be moved/copied by the command." };
    var inplaceOption = new CliOption<bool>("-i", "--inplace") { Description = "Renames the files in place, rather than to <DESTINATION>." };

    var rootCommand = new CliRootCommand
    {
        sourceArgument,
        moviesOption,
        tvOption,
        moveOption,
        recursiveOption,
        dryRunOption,
        inplaceOption,
    };

    var serviceCollection = new ServiceCollection();
    serviceCollection.AddLogging(builder =>
    {
        var configuration = new LoggerConfiguration();
        configuration.WriteTo.Console(formatProvider: System.Globalization.CultureInfo.CurrentCulture);
#if DEBUG
        configuration.MinimumLevel.Debug();
#endif
        builder.AddSerilog(configuration.CreateLogger());
    });

    var services = serviceCollection.BuildServiceProvider();

    rootCommand.SetAction(parseResult =>
        Process(
            services.GetRequiredService<ILoggerFactory>().CreateLogger("Program"),
            parseResult.GetValue(sourceArgument)!,
            parseResult.GetValue(moviesOption)!,
            parseResult.GetValue(tvOption)!,
            parseResult.GetValue(moveOption),
            parseResult.GetValue(recursiveOption),
            parseResult.GetValue(dryRunOption),
            parseResult.GetValue(inplaceOption)));

    return new CliConfiguration(rootCommand);
}

static void Process(
    Microsoft.Extensions.Logging.ILogger logger,
    DirectoryInfo source,
    DirectoryInfo movies,
    DirectoryInfo tv,
    bool move = false,
    bool recursive = false,
    bool dryRun = false,
    bool inplace = false)
{
    var episodeNumberByteVector = (ReadOnlyByteVector)"tves";
    var showNameByteVector = (ReadOnlyByteVector)"tvsh";
    var seasonNumberByteVector = (ReadOnlyByteVector)"tvsn";
    var workByteVector = new ReadOnlyByteVector([ 169, 119, 114, 107 ], 4);
    var contentIdByteVector = (ReadOnlyByteVector)"cnID";

    tv ??= movies;
    movies ??= tv;

    // search for all files in the source directory
    foreach (var file in source.EnumerateFiles("*.*", new EnumerationOptions { RecurseSubdirectories = recursive, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.Hidden }))
    {
        if (file.Length == 0)
        {
            continue;
        }

        TagLib.File? tagLibFile = default;
        FileInfo? path = default;

        try
        {
            tagLibFile = TagLib.File.Create(file.FullName);

            if (tagLibFile.GetTag(TagTypes.Apple) is TagLib.Mpeg4.AppleTag appleTag)
            {
                if (appleTag.IsMovie())
                {
                    var directory = inplace
                        ? file.GetDirectoryName(GetInvalidPathChars())
                        : Path.Combine(movies.FullName, "Movies").ReplaceAll(GetInvalidPathChars());

                    var fileNameWithoutExtension = $"{appleTag.Title.Sanitise()} ({appleTag.Year})";
                    if (appleTag.TryGetString(workByteVector, out var work))
                    {
                        if (!inplace)
                        {
                            directory = Path.Combine(directory, fileNameWithoutExtension.ReplaceAll(GetInvalidPathChars()));
                        }

                        work = work.Trim();
                        if (work.Length != 0)
                        {
                            fileNameWithoutExtension += " - ";
                            fileNameWithoutExtension += work;
                        }
                    }

                    var fileName = (fileNameWithoutExtension + file.Extension).ReplaceAll(GetInvalidFileNameChars());
                    path = new FileInfo(Path.Combine(directory, fileName));
                }
                else if (appleTag.IsTvShow())
                {
                    var showName = string.Join("; ", appleTag.GetText(showNameByteVector)).Sanitise();
                    var seasonNumber = appleTag.GetUInt32(seasonNumberByteVector);
                    var episodeNumber = appleTag.GetUInt32(episodeNumberByteVector);

                    var directory = inplace
                        ? file.GetDirectoryName(GetInvalidPathChars())
                        : Path.Combine(tv.FullName, "TV Shows", showName, $"Season {seasonNumber:00}").ReplaceAll(GetInvalidPathChars());
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
                    path = new FileInfo(Path.Combine(directory, fileName));
                }
                else
                {
                    logger.LogInformation("Failed to match {File} to either Movie or TV Show", file.Name);
                }
            }
            else
            {
                logger.LogDebug("Found non 'Apple' format at {File}", file.Name);
            }

            tagLibFile.Mode = TagLib.File.AccessMode.Closed;
        }
        catch (UnsupportedFormatException)
        {
            logger.LogDebug("Unsupported file - {File}", file.Name);
        }
        catch (CorruptFileException)
        {
            logger.LogDebug("Corrupt file - {File}", file.Name);
        }
        finally
        {
            tagLibFile?.Dispose();
        }

        if (path != null)
        {
            if (path.Exists && path.Length == file.Length)
            {
                logger.LogDebug("{Source} has the same length as {Destination}", file.FullName, path.FullName);
                continue;
            }

            if (!dryRun && path.Directory is not null)
            {
                path.Directory.Create();
            }

            if (move)
            {
                if (!path.Exists || inplace)
                {
                    logger.LogInformation("Moving {Source} to {Destination}", file.FullName, path.FullName);
                    if (!dryRun)
                    {
                        file.MoveTo(path.FullName);
                    }
                }
                else
                {
                    logger.LogInformation("Replacing {Destination} with {Source} with a move", path.FullName, file.FullName);
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
                    logger.LogInformation("Replacing {Destination} with {Source} by a copy", path.FullName, file.FullName);
                }
                else
                {
                    logger.LogInformation("Coping {Source} to {Destination}", file.FullName, path.FullName);
                }

                if (!dryRun)
                {
                    file.CopyTo(path.FullName, true);
                }
            }
        }
    }
}