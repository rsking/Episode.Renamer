//-----------------------------------------------------------------------
// <copyright file="ExtensionMethods.cs" company="RossKing">
// Copyright (c) RossKing. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Episode.Renamer;

/// <summary>
/// Extension methods.
/// </summary>
internal static class ExtensionMethods
{
    private const char ReplacementChar = '_';

    private static readonly TagLib.ReadOnlyByteVector StikAtom = "stik";

    /// <summary>
    /// Gets the <see cref="uint" /> from the <see cref="TagLib.Mpeg4.AppleTag" /> using the specified type.
    /// </summary>
    /// <param name="appleTag">The apple tag.</param>
    /// <param name="type">The type.</param>
    /// <returns>The value.</returns>
    public static uint GetUInt32(this TagLib.Mpeg4.AppleTag appleTag, TagLib.ReadOnlyByteVector type)
    {
        var item = appleTag.DataBoxes(type).FirstOrDefault(item => item.Data.Count == 4);
        if (item is null)
        {
            return default;
        }

        return GetUInt32(item.Data.Data);
    }

    /// <summary>
    /// Tries to gets a <see cref="uint" /> from the <see cref="TagLib.Mpeg4.AppleTag" /> using the specified type.
    /// </summary>
    /// <param name="appleTag">The apple tag.</param>
    /// <param name="type">The meta data type.</param>
    /// <param name="value">The <see cref="uint" /> value, if successful.</param>
    /// <returns><see langword="true" /> if <paramref name="appleTag"/> contains <paramref name="type"/>; otherwise <see langword="false"/>.</returns>
    public static bool TryGetUInt32(this TagLib.Mpeg4.AppleTag appleTag, TagLib.ReadOnlyByteVector type, out uint value)
    {
        var item = appleTag.DataBoxes(type).FirstOrDefault(item => item.Data.Count == 4);
        if (item is null)
        {
            value = default;
            return false;
        }

        value = GetUInt32(item.Data.Data);
        return true;
    }

    /// <summary>
    /// Gets a value indicating whether this <see cref="TagLib.Mpeg4.AppleTag" /> represents a movie.
    /// </summary>
    /// <param name="appleTag">The apple tag.</param>
    /// <returns><see langword="true" /> if <paramref name="appleTag"/> represents a movie.</returns>
    public static bool IsMovie(this TagLib.Mpeg4.AppleTag appleTag) => appleTag.DataBoxes(StikAtom).Any(item => item.Data.Count == 1 && item.Data.Data[0] == 9);

    /// <summary>
    /// Gets a value indicating whether this <see cref="TagLib.Mpeg4.AppleTag" /> represents a TV show.
    /// </summary>
    /// <param name="appleTag">The apple tag.</param>
    /// <returns><see langword="true" /> if <paramref name="appleTag"/> represents a TV show.</returns>
    public static bool IsTvShow(this TagLib.Mpeg4.AppleTag appleTag) => appleTag.DataBoxes(StikAtom).Any(item => item.Data.Count == 1 && item.Data.Data[0] == 10);

    /// <summary>
    /// Tries to gets a <see cref="string" /> from the <see cref="TagLib.Mpeg4.AppleTag" /> using the specified type.
    /// </summary>
    /// <param name="appleTag">The apple tag.</param>
    /// <param name="type">The meta data type.</param>
    /// <param name="value">The <see cref="string" /> value, if successful.</param>
    /// <returns><see langword="true" /> if <paramref name="appleTag"/> contains <paramref name="type"/>; otherwise <see langword="false"/>.</returns>
    public static bool TryGetString(this TagLib.Mpeg4.AppleTag appleTag, TagLib.ReadOnlyByteVector type, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)
    {
        var text = appleTag.GetText(type);
        if (text is null || text.Length == 0)
        {
            value = default;
            return false;
        }

        value = text[0];
        return true;
    }

    /// <summary>
    /// Replaces all instances of <paramref name="oldValues" /> with <paramref name="newValue" />.
    /// </summary>
    /// <param name="stringValue">The string value.</param>
    /// <param name="oldValues">The old values.</param>
    /// <param name="newValue">The new value.</param>
    /// <returns><paramref name="stringValue"/> with all instances of <paramref name="oldValues" /> with <paramref name="newValue" />.</returns>
    public static string ReplaceAll(this string stringValue, char[] oldValues, char newValue = ReplacementChar)
    {
        foreach (var oldValue in oldValues)
        {
            stringValue = stringValue.Replace(oldValue, newValue);
        }

        return stringValue;
    }

    /// <summary>
    /// Sanitises the string, changing exotic characters, to common ones, such as smart quotes.
    /// </summary>
    /// <param name="stringValue">The string value.</param>
    /// <returns>A sanitised version of <paramref name="stringValue"/>.</returns>
    public static string Sanitise(this string stringValue) => stringValue
        .Replace('’', '\'').Replace('‘', '\'') // single quotes
        .Replace('“', '"').Replace('”', '"') // double quotes
        .Replace(System.IO.Path.DirectorySeparatorChar, ReplacementChar); // possible directory separators

    /// <summary>
    /// Gets the directory name.
    /// </summary>
    /// <param name="fileInfo">The file information.</param>
    /// <param name="invalidCharacters">The invalid characters.</param>
    /// <returns>The directory name.</returns>
    public static string GetDirectoryName(this FileInfo fileInfo, char[] invalidCharacters)
    {
        if (fileInfo.DirectoryName is null)
        {
            return string.Empty;
        }

        return fileInfo.DirectoryName.ReplaceAll(invalidCharacters);
    }

#if !NETCOREAPP
    public static void MoveTo(this FileInfo fileInfo, string destFileName, bool overwrite)
    {
        if (overwrite)
        {
            fileInfo.Replace(destFileName, null);
        }
        else
        {
            fileInfo.MoveTo(destFileName);
        }
    }
#endif

    private static uint GetUInt32(byte[] data)
    {
        var bytes = System.BitConverter.IsLittleEndian
            ? SwitchBytes(data)
            : data;

        return System.BitConverter.ToUInt32(bytes);

        static byte[] SwitchBytes(byte[] data)
        {
            return new[] { data[3], data[2], data[1], data[0] };
        }
    }
}