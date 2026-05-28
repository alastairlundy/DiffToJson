namespace DiffToJsonCli.Helpers;

internal class LicenseFileFinder
{
    internal static async Task<FileInfo?> FindLicenseFile(string workingDir)
    {
        string[] priorityFiles = ["LICENSE.md", "LICENSE.txt", "LICENSE"];
        
        foreach (string fileName in priorityFiles)
        {
            string path = Path.Combine(workingDir, fileName);
            if (File.Exists(path))
            {
                return new FileInfo(path);
            }
        }

        return null;
    }
}