using System.CommandLine;
using GitDiffToJsonLCli;

Option<DirectoryInfo> repoDirectoryOption = new("--repo-directory")
{
    Description = "The local git repository directory. Falls back to the current directory if not provided."
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

RootCommand rootCommand = new("Git Diff to JSONL converter")
{
    repoDirectoryOption,
    repoUrlOption,
    modelIdOption,
    endpointUrlOption,
    providerOption,
    apiKeyOption,
};

rootCommand.SetAction(async result =>
{
    try
    {
        DirectoryInfo targetDir = result.GetValue(repoDirectoryOption) ?? new DirectoryInfo(Directory.GetCurrentDirectory());
        string repoUrl = result.GetValue(repoUrlOption) ?? "";
        string repoName = targetDir.Name;
        string outputPath = $"{repoName}-commits.jsonl";

        Console.WriteLine($"Analyzing repository: {repoName} at {targetDir.FullName}");
        
        string provider = result.GetValue(providerOption) ?? "";
        string apiKey = result.GetValue(apiKeyOption) ?? "";
        string? endpointUrl = result.GetValue(endpointUrlOption);
        string? modelId = result.GetValue(modelIdOption);
        
        ArgumentException.ThrowIfNullOrEmpty(endpointUrl);
        ArgumentException.ThrowIfNullOrEmpty(modelId);
        
        LicenseAnalyzer analyzer = new(provider, endpointUrl, modelId, apiKey);
        string license = await analyzer.AnalyzeLicenseAsync(targetDir.FullName);
        await Console.Out.WriteLineAsync($"Detected License: {license}");

        GitParser parser = new GitParser();
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