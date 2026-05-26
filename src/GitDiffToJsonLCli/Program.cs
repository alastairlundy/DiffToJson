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

using System.CommandLine;
using GitDiffToJsonLCli;

Option<DirectoryInfo> repoDirectoryOption = new("--repo-directory")
{
    Description = "The local git repository directory. Falls back to the current directory if not provided.",
    Required = false
};

Option<string> repoUrlOption = new("--repo-url")
{
    Description = "The URL of the git repository.",
    Required = false
};

Option<string> modelIdOption = new("--model-id")
{
    Description = "The model id of the AI model to use",
    Required = true
};

Option<string> endpointUrlOption = new("--endpoint-url")
{
    Description = "The endpoint URL of the OpenAI compatible API endpoint to use.",
    Required = true
};

Option<string> apiKeyOption = new("--api-key")
{
    Description = "The API key of the AI provider to use.",
    Required = false,
};

Option<string> providerOption = new("--provider")
{
    Description = "The Id of the AI provider to use.",
    Required = false,
};

Option<string> licenseOption = new("--license")
{
    Description = "The licence name to use for the JSON.",
    Required = false,
    DefaultValueFactory = _ => ""
};

Option<string> outputFilePathOption = new("--output-file", ["-o"])
{
    Description = "The output file path. If not specified, the default is the repository directory path.",
    Required = false,
    DefaultValueFactory = _ => ""
};

RootCommand rootCommand = new("Detects and Serializes Git Diff and Commits to a .JSONL file.")
{
    repoDirectoryOption,
    repoUrlOption,
    modelIdOption,
    endpointUrlOption,
    providerOption,
    apiKeyOption,
    licenseOption,
    outputFilePathOption
};

rootCommand.SetAction(async result =>
{
    try
    {
        DirectoryInfo targetDir = result.GetValue(repoDirectoryOption) ?? new DirectoryInfo(Directory.GetCurrentDirectory());
        string repoUrl = result.GetValue(repoUrlOption) ?? "";
        string repoName = targetDir.Name;

        string outputFilePath = result.GetValue(outputFilePathOption) ?? "";
        
        string outputPath;
        if (string.IsNullOrEmpty(outputFilePath))
        {
            outputPath = $"{targetDir.FullName}{Path.DirectorySeparatorChar}{repoName}-commits.jsonl";
        }
        else
        {
            DirectoryInfo directoryInfo = new(outputFilePath);

            outputPath = Path.Combine(directoryInfo.FullName, $"{repoName}-commits.jsonl");
        }

        Console.WriteLine($"Analyzing repository: {targetDir.Name} at {targetDir.FullName}");
        
        string provider = result.GetValue(providerOption) ?? "";
        string apiKey = result.GetValue(apiKeyOption) ?? "";
        string? endpointUrl = result.GetValue(endpointUrlOption);
        string? modelId = result.GetValue(modelIdOption);
        
        string licenseProvided = result.GetValue(licenseOption) ?? "";

        string license;
        
        if (string.IsNullOrEmpty(licenseProvided))
        {
            ArgumentException.ThrowIfNullOrEmpty(endpointUrl);
            ArgumentException.ThrowIfNullOrEmpty(modelId);
            
            LicenseAnalyzer analyzer = new(provider, endpointUrl, modelId, apiKey); 
            license = await analyzer.AnalyzeLicenseAsync(targetDir.FullName);
            await Console.Out.WriteLineAsync($"Detected License: {license}");
        }
        else
        {
            license = licenseProvided;
            await Console.Out.WriteLineAsync($"Using specified License: {license}");
        }

        GitParser parser = new();
        await parser.ParseStreamingAsync(repoName, license, outputPath, targetDir.FullName, repoUrl);

        await Console.Out.WriteLineAsync($"Successfully wrote commits to {outputPath}");
    }
    catch (Exception ex)
    {
        await Console.Error.WriteLineAsync($"Error: {ex.Message}");
        Environment.Exit(1);
    }
});

ParseResult parseResult = rootCommand.Parse(args);

return await parseResult.InvokeAsync();