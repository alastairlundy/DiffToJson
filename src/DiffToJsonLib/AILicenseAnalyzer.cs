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

using Microsoft.Extensions.AI;

namespace DiffToJsonLib;

public class AILicenseAnalyzer : ILicenseAnalyzer
{
    private readonly IChatClient _client;

    public AILicenseAnalyzer(IChatClient chatClient)
    {
        _client = chatClient;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="licenseFile"></param>
    /// <returns></returns>
    /// <exception cref="FileNotFoundException"></exception>
    /// <exception cref="NullReferenceException"></exception>
    public async Task<string?> AnalyzeLicenseAsync(FileInfo licenseFile)
    {
        if(!licenseFile.Exists)
            throw new FileNotFoundException("File not found", licenseFile.FullName);
        
        string licenseContent = await File.ReadAllTextAsync(licenseFile.FullName);
        
        if (string.IsNullOrEmpty(licenseContent))
        {
            throw new NullReferenceException($"License content in directory '{licenseFile}' was null or empty.");
        }

        ChatMessage systemPrompt = new(ChatRole.System, 
            """
            You are an expert open source contributor. Analyse the following open source licence text and return only the name of the licence (e.g. 'MIT', 'Apache 2.0', 'GPL-3.0').
            Do not include the word 'licence' in your answer.
            """);

        ChatMessage userPrompt = new(ChatRole.User, licenseContent);
        
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
            return null;
        
#if DEBUG
        await Console.Out.WriteLineAsync($"RAW Detected: {message}");
        await Console.Out.WriteLineAsync($"Trimmed License: {trimmed}");
#endif
        
        return trimmed;
    }
}