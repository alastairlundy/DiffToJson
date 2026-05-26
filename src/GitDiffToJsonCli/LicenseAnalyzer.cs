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

using System.ClientModel;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;

namespace GitDiffToJsonCli;

public class LicenseAnalyzer
{
    private readonly IChatClient _client;

    public LicenseAnalyzer(string provider, string endpoint, string model, string apiKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(endpoint);
        ArgumentException.ThrowIfNullOrEmpty(model);
        
        if(string.IsNullOrEmpty(provider))
            ArgumentException.ThrowIfNullOrEmpty(apiKey);
        
        switch (provider.ToLower())
        {
            case "ollama":
            {
                OllamaApiClient.Configuration configuration = new OllamaApiClient.Configuration
                {
                    JsonSerializerContext = CustomOllamaJsonContext.Default,
                    Model = model,
                    Uri = new Uri(endpoint)
                };
                _client = new OllamaApiClient(configuration);

                break;
            }
            default:
            {
                _client = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions
                    {
                        Endpoint = new  Uri(endpoint)
                    }) .GetChatClient(model)
                    .AsIChatClient();
                
                break;
            }
        }
    }

    public async Task<string> AnalyzeLicenseAsync(string workingDir)
    {
        await Console.Out.WriteLineAsync($"Checking for license file in {workingDir}");
        string? licenseContent = FindLicenseFile(workingDir);

#if DEBUG
        await Console.Out.WriteLineAsync($"License file content length: {licenseContent?.Length ?? 0}");
#endif
        
        if (string.IsNullOrEmpty(licenseContent))
        {
            throw new NullReferenceException($"License content in directory '{workingDir}' was null or empty.");
        }

        ChatMessage systemPrompt = new(ChatRole.System, 
            """
            You are an expert open source contributor. Analyse the following open source licence text and return only the name of the licence (e.g. 'MIT', 'Apache 2.0', 'GPL-3.0').
            Do not include the word 'licence' in your answer.
            """);

        ChatMessage userPrompt = new(ChatRole.User, licenseContent);
            
        await Console.Out.WriteLineAsync("Asking LLM for licence info");
            
        ChatResponse response = await _client.GetResponseAsync([systemPrompt, userPrompt], new ChatOptions
        {
            Temperature = 0.6f
        });
    
#if DEBUG
        for (int index = 0; index < response.Messages.Count; index++)
        {
            var msg = response.Messages[index];
            await Console.Out.WriteLineAsync($"Message Number {index + 1} Role: {msg.Role} Text: {msg.Text}");
        }
        await Console.Out.WriteLineAsync("");
#endif
    
        string? message = response.Messages
            .FirstOrDefault(m => m.Role == ChatRole.Assistant)?.Text;
    
        string? trimmed = message?.Trim();

        if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(trimmed))
            return "Unknown";
        
#if DEBUG
        await Console.Out.WriteLineAsync($"RAW Detected: {message}");
        await Console.Out.WriteLineAsync($"Trimmed License: {trimmed}");
#else
        await Console.Out.WriteLineAsync($"Detected License: {trimmed}");
#endif
        
        return trimmed;
    }

    private static string? FindLicenseFile(string workingDir)
    {
        string[] priorityFiles = ["LICENSE.md", "LICENSE.txt", "LICENSE"];
        
        foreach (string fileName in priorityFiles)
        {
            string path = Path.Combine(workingDir, fileName);
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }
        return null;
    }
}