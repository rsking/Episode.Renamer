//-----------------------------------------------------------------------
// <copyright file="Program.cs" company="RossKing">
// Copyright (c) RossKing. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Episode.Renamer
{
    using System.CommandLine;
    using System.CommandLine.Builder;
    using System.CommandLine.Hosting;
    using System.CommandLine.Invocation;
    using System.CommandLine.Parsing;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Serilog;
    using TagLib;

    /// <summary>
    /// The main program.
    /// </summary>
    internal class Program
    {
        private static readonly ReadOnlyByteVector EpisodeNumber = "tves";
        private static readonly ReadOnlyByteVector ShowName = "tvsh";
        private static readonly ReadOnlyByteVector SeasonNumber = "tvsn";

        private static char[] GetInvalidFileNameChars() => new char[]
        {
            '\"', '<', '>', '|', '\0',
            (char)1, (char)2, (char)3, (char)4, (char)5, (char)6, (char)7, (char)8, (char)9, (char)10,
            (char)11, (char)12, (char)13, (char)14, (char)15, (char)16, (char)17, (char)18, (char)19, (char)20,
            (char)21, (char)22, (char)23, (char)24, (char)25, (char)26, (char)27, (char)28, (char)29, (char)30,
            (char)31, ':', '*', '?', '\\', '/',
        };

        private static char[] GetInvalidPathChars() => new char[]
        {
            '\"', '<', '>', '|', '\0',
            (char)1, (char)2, (char)3, (char)4, (char)5, (char)6, (char)7, (char)8, (char)9, (char)10,
            (char)11, (char)12, (char)13, (char)14, (char)15, (char)16, (char)17, (char)18, (char)19, (char)20,
            (char)21, (char)22, (char)23, (char)24, (char)25, (char)26, (char)27, (char)28, (char)29, (char)30,
            (char)31, ':', '*', '?',
        };

        private static Task<int> Main(string[] args)
        {
            return BuildCommandLine()
                .UseHost(
                    _ => Host.CreateDefaultBuilder().UseContentRoot(System.AppDomain.CurrentDomain.BaseDirectory),
                    host => host.UseSerilog((hostBuilderContext, loggerConfiguration) => loggerConfiguration.ReadFrom.Configuration(hostBuilderContext.Configuration)))
                .UseDefaults()
                .Build()
                .InvokeAsync(args.Select(arg => System.Environment.ExpandEnvironmentVariables(arg)).ToArray());

            static CommandLineBuilder BuildCommandLine()
            {
                var root = new RootCommand();
                root.AddArgument(new Argument<System.IO.DirectoryInfo>("source"));
                root.AddArgument(new Argument<System.IO.DirectoryInfo>("destination"));
                root.AddOption(new Option<bool>(new[] { "-m", "--move" }, "Moves the files"));
                root.AddOption(new Option<bool>(new[] { "-r", "--recursive" }, "Recursively searches <SOURCE>"));
                root.AddOption(new Option<bool>(new[] { "-n", "--dry-run" }, "Don’t actually move/copy any file(s). Instead, just show if they exist and would otherwise be moved/copied by the command."));
                root.AddOption(new Option<bool>(new[] { "-i", "--inplace" }, "Renames the files in place, rather than to <DESTINATION>."));

                root.Handler = CommandHandler.Create<IHost, System.IO.DirectoryInfo, System.IO.DirectoryInfo, bool, bool, bool, bool>(Process);

                return new CommandLineBuilder(root);
            }

            static void Process(
                IHost host,
                System.IO.DirectoryInfo source,
                System.IO.DirectoryInfo destination,
                bool move = false,
                bool recursive = false,
                bool dryRun = false,
                bool inplace = false)
            {
                // search for all files in the source directory
                var programLogger = host.Services.GetRequiredService<ILogger<Program>>();

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
                                    : System.IO.Path.Combine(destination.FullName, "Movies").ReplaceAll(GetInvalidPathChars());
                                var fileName = $"{appleTag.Title} ({appleTag.Year}){file.Extension}".ReplaceAll(GetInvalidFileNameChars());
                                path = new System.IO.FileInfo(System.IO.Path.Combine(directory, fileName));
                            }
                            else if (appleTag.IsTvShow())
                            {
                                var showName = string.Join("; ", appleTag.GetText(ShowName));
                                var seasonNumber = appleTag.GetUInt32(SeasonNumber);
                                var episodeNumber = appleTag.GetUInt32(EpisodeNumber);
                                var episodeName = appleTag.Title;

                                var directory = inplace
                                    ? file.DirectoryName.ReplaceAll(GetInvalidPathChars())
                                    : System.IO.Path.Combine(destination.FullName, "TV Shows", showName, $"Season {seasonNumber:00}").ReplaceAll(GetInvalidPathChars());
                                var fileName = $"{showName} - s{seasonNumber:00}e{episodeNumber:00} - {episodeName}{file.Extension}".ReplaceAll(GetInvalidFileNameChars());

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
                            programLogger.LogInformation("Moving {Source} to {Destination}", file.FullName, path.FullName);
                            if (!dryRun)
                            {
                                if (!path.Exists || inplace)
                                {
                                    file.MoveTo(path.FullName);
                                }
                                else
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
                            programLogger.LogInformation("Coping {Source} to {Destination}", file.FullName, path.FullName);
                            if (!dryRun)
                            {
                                file.CopyTo(path.FullName, true);
                            }
                        }
                    }
                }
            }
        }
    }
}
