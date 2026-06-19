/*
    Copyright 2026 Alastair Lundy

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
 */

namespace DiffToJsonCli.Helpers;

internal class LicenseFileFinder
{
    internal static Task<FileInfo?> FindLicenseFile(string workingDir)
    {
        try
        {
            string[] priorityFiles = ["LICENSE.md", "LICENSE.txt", "LICENSE"];
        
            foreach (string fileName in priorityFiles)
            {
                string path = Path.Combine(workingDir, fileName);
                if (File.Exists(path))
                {
                    FileInfo fileInfo = new(path);
                    
                    return Task.FromResult((FileInfo?)fileInfo);
                }
            }

            return Task.FromResult<FileInfo?>(null);
        }
        catch (Exception exception)
        {
            return Task.FromException<FileInfo?>(exception);
        }
    }
}