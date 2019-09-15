namespace Episode.Renamer
{
    internal static class ExtensionMethods
    {
        private static readonly TagLib.ReadOnlyByteVector StikAtom = "stik";

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

        public static string ReplaceAll(this string stringValue, char[] oldValues, char newValue)
        {
            foreach (var oldValue in oldValues)
            {
                stringValue = stringValue.Replace(oldValue, newValue);
            }

            return stringValue;
        }

#if !NETCOREAPP3_0
        public static void MoveTo(this System.IO.FileInfo fileInfo, string destFileName, bool overwrite)
        {
            if (overwrite)
            {
                var tempFileName = System.IO.Path.GetTempFileName();
                fileInfo.Replace(destFileName, null);
                System.IO.File.Delete(tempFileName);
            }
            else
            {
                fileInfo.MoveTo(destFileName);
            }
        }
#endif
    }
}
