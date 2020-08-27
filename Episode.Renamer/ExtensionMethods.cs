//-----------------------------------------------------------------------
// <copyright file="ExtensionMethods.cs" company="RossKing">
// Copyright (c) RossKing. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

namespace Episode.Renamer
{
    /// <summary>
    /// Extension methods.
    /// </summary>
    internal static class ExtensionMethods
    {
        private static readonly TagLib.ReadOnlyByteVector StikAtom = "stik";

        /// <summary>
        /// Gets the <see cref="uint" /> from the <see cref="TagLib.Mpeg4.AppleTag" /> using the specified type.
        /// </summary>
        /// <param name="appleTag">The apple tag.</param>
        /// <param name="type">The type.</param>
        /// <returns>The value.</returns>
        public static uint GetUInt32(this TagLib.Mpeg4.AppleTag appleTag, TagLib.ReadOnlyByteVector type)
        {
            foreach (var item in appleTag.DataBoxes(type))
            {
                if (item.Data.Count == 4)
                {
                    byte[] data;
                    if (System.BitConverter.IsLittleEndian)
                    {
                        data = new byte[item.Data.Count];
                        data[0] = item.Data.Data[3];
                        data[1] = item.Data.Data[2];
                        data[2] = item.Data.Data[1];
                        data[3] = item.Data.Data[0];
                    }
                    else
                    {
                        data = item.Data.Data;
                    }

                    return System.BitConverter.ToUInt32(data);
                }
            }

            return 0u;
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="TagLib.Mpeg4.AppleTag" /> represents a movie.
        /// </summary>
        /// <param name="appleTag">The apple tag.</param>
        /// <returns><see langword="true" /> if <paramref name="appleTag"/> represents a movie.</returns>
        public static bool IsMovie(this TagLib.Mpeg4.AppleTag appleTag)
        {
            foreach (var item in appleTag.DataBoxes(StikAtom))
            {
                if (item.Data.Count == 1)
                {
                    return item.Data.Data[0] == 9;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="TagLib.Mpeg4.AppleTag" /> represents a TV show.
        /// </summary>
        /// <param name="appleTag">The apple tag.</param>
        /// <returns><see langword="true" /> if <paramref name="appleTag"/> represents a TV show.</returns>
        public static bool IsTvShow(this TagLib.Mpeg4.AppleTag appleTag)
        {
            foreach (var item in appleTag.DataBoxes(StikAtom))
            {
                if (item.Data.Count == 1)
                {
                    return item.Data.Data[0] == 10;
                }
            }

            return false;
        }

        /// <summary>
        /// Trties to gets a string from the specified type.
        /// </summary>
        /// <param name="appleTag">The apple tag.</param>
        /// <param name="type">The meta data type.</param>
        /// <param name="value">The string value, if successful.</param>
        /// <returns><see langword="true" /> if <paramref name="appleTag"/> contains <paramref name="type"/>; otherwise <see langword="false"/>.</returns>
        public static bool TryGetString(this TagLib.Mpeg4.AppleTag appleTag, TagLib.ReadOnlyByteVector type, out string value)
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
        public static string ReplaceAll(this string stringValue, char[] oldValues, char newValue = '_')
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
            .Replace('“', '"').Replace('”', '"'); // double quotes

#if !NETCOREAPP
        public static void MoveTo(this System.IO.FileInfo fileInfo, string destFileName, bool overwrite)
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
    }
}
