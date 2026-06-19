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
using CliInvoke;
using CliInvoke.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Compliance.Redaction;
using DiffToJsonLib.Redactors;

IServiceCollection services = new  ServiceCollection();

services.AddSingleton<IProcessInvoker, ProcessInvoker>();
services.AddRedaction(redaction =>
{
    redaction.SetFallbackRedactor<RegexPiiRedactor>();
});

services.AddSingleton<IDiffJsonFileWriter, DiffJsonFileWriter>();
services.AddSingleton<IGitCommitParser, GitCommitParser>();


Option<DirectoryInfo> repoDirectoryOption = new("--repo-directory")
{
    Description = "The local git repository directory. Falls back to the current directory if not provided.",
    DefaultValueFactory = _ => new DirectoryInfo(Directory.GetCurrentDirectory()),
    Required = false
};

Option<string> repoUrlOption = new("--repo-url")
{
    Description = "The URL of the git repository.",
    DefaultValueFactory = _ => "",
    Required = false
};

Option<string> modelIdOption = new("--model-id")
{
    Description = "The model id of the AI model to use",
    DefaultValueFactory = _ => "",
    Required = false
};

Option<string> endpointUrlOption = new("--endpoint-url")
{
    Description = "The endpoint URL of the OpenAI compatible API endpoint to use.",
    DefaultValueFactory = _ => "",
    Required = false
};

Option<string> apiKeyOption = new("--api-key")
{
    Description = "The API key of the AI provider to use.",
    DefaultValueFactory = _ => "",
    Required = false,
};

Option<string> providerOption = new("--provider")
{
    Description = "The Id of the AI provider to use.",
    DefaultValueFactory = _ => "",
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

RootCommand rootCommand = new("Detects and Serializes Git Diffs and Commits to a .JSONL file.")
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

            outputPath = outputFilePath.EndsWith(".jsonl") ?
                Path.Combine(directoryInfo.FullName, outputFilePath) :
                Path.Combine(directoryInfo.FullName, $"{repoName}-commits.jsonl");
        }

        Console.WriteLine($"Analyzing repository: {targetDir.Name} at {targetDir.FullName}");
        
        string provider = result.GetValue(providerOption) ?? "";
        string apiKey = result.GetValue(apiKeyOption) ?? "";
        string? endpointUrl = result.GetValue(endpointUrlOption);
        string? modelId = result.GetValue(modelIdOption);
        
        string licenseProvided = result.GetValue(licenseOption) ?? "";

        string license;

        IServiceProvider serviceProvider;
        
        if (string.IsNullOrEmpty(licenseProvided))
        {
            ArgumentException.ThrowIfNullOrEmpty(endpointUrl);
            ArgumentException.ThrowIfNullOrEmpty(modelId);

            services.AddSingleton<IChatClientFactory>(sp =>
                new ChatClientFactory(provider, apiKey, endpointUrl, modelId));
            services.AddSingleton<ILicenseAnalyzer, AILicenseAnalyzer>();
            serviceProvider = services.BuildServiceProvider();
            
            ILicenseAnalyzer licenseAnalyzer = serviceProvider.GetRequiredService<ILicenseAnalyzer>();
            
            FileInfo? fileInfo = await LicenseFileFinder.FindLicenseFile(targetDir.FullName);
            
            if(fileInfo is not null)
                license = await licenseAnalyzer.AnalyzeLicenseAsync(fileInfo) ?? "Unknown";
            else
                license = "Unknown";
            
            await Console.Out.WriteLineAsync($"Detected License: {license}");
        }
        else
        {
            serviceProvider = services.BuildServiceProvider();
            license = licenseProvided;
            await Console.Out.WriteLineAsync($"Using specified License: {license}");
        }

        IGitCommitParser commitParser = serviceProvider.GetRequiredService<IGitCommitParser>();
        IDiffJsonFileWriter diffJsonFileWriter = serviceProvider.GetRequiredService<IDiffJsonFileWriter>();

        IAsyncEnumerable<CommitRecord> records = commitParser.ParseCommitsStreamAsync(repoName, license, targetDir.FullName,
            repoUrl, CancellationToken.None);

        await diffJsonFileWriter.WriteToJsonFileAsync(records, outputFilePath, CancellationToken.None);

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