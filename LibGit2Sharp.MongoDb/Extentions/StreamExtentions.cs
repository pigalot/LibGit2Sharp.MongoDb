namespace LibGit2Sharp.MongoDb.Extentions
{
    using System.IO;

    public static class StreamExtentions
    {
        public static byte[] ReadAsBytes(this Stream stream)
        {
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                return memoryStream.ToArray();
            }
        }
    }
}
