namespace LibGit2Sharp.MongoDb.Test
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text;

    public class Helpers
    {
        public static void DeleteDirectory(string directoryPath)
        {
            // From http://stackoverflow.com/questions/329355/cannot-delete-directory-with-directory-deletepath-true/329502#329502
            if (!Directory.Exists(directoryPath))
            {
                Trace.WriteLine($"Directory '{directoryPath}' is missing and can't be removed.");
                return;
            }

            var files = Directory.GetFiles(directoryPath);
            var dirs = Directory.GetDirectories(directoryPath);

            foreach (var file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            foreach (var dir in dirs)
            {
                DeleteDirectory(dir);
            }

            File.SetAttributes(directoryPath, FileAttributes.Normal);
            try
            {
                Directory.Delete(directoryPath, false);
            }
            catch (IOException)
            {
                Trace.WriteLine($"{Environment.NewLine}The directory '{Path.GetFullPath(directoryPath)}' could not be deleted!");
            }
        }

        public static string Touch(string parent, string file, string content = null, Encoding encoding = null)
        {
            string filePath = Path.Combine(parent, file);
            string dir = Path.GetDirectoryName(filePath);
            Debug.Assert(dir != null, $"Directory for {filePath} not found.");

            Directory.CreateDirectory(dir);

            File.WriteAllText(filePath, content ?? string.Empty, encoding ?? Encoding.ASCII);

            return filePath;
        }
    }
}
