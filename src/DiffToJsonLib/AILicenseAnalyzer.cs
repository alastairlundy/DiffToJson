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

using System.IO.Abstractions;
using Microsoft.Extensions.AI;

namespace DiffToJsonLib;

public class AILicenseAnalyzer : ILicenseAnalyzer
{
    private readonly Lazy<IChatClient> _clientLazy;
    private readonly IFileSystem _fileSystem;

    public AILicenseAnalyzer(IChatClientFactory chatClientFactory, IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(chatClientFactory);
        ArgumentNullException.ThrowIfNull(fileSystem);
        _clientLazy = new Lazy<IChatClient>(chatClientFactory.Create);
        _fileSystem = fileSystem;
    }

    /// <summary>
    /// Sends the license file's text to the configured chat client and returns the
    /// trimmed licence name, or null when the model returns an empty response.
    /// </summary>
    public async Task<string?> AnalyzeLicenseAsync(IFileInfo licenseFile)
    {
        if(!licenseFile.Exists)
            throw new FileNotFoundException("File not found", licenseFile.FullName);
        
        string licenseContent = await _fileSystem.File.ReadAllTextAsync(licenseFile.FullName).ConfigureAwait(false);
        
        if (string.IsNullOrEmpty(licenseContent))
        {
            throw new ArgumentNullException(nameof(licenseFile), $"License content in directory '{licenseFile}' was null or empty.");
        }

        ChatMessage systemPrompt = new(ChatRole.System, 
            """
            You are an expert open source contributor. Analyse the following open source licence text and return only the name of the licence (e.g. 'MIT', 'Apache 2.0', 'GPL-3.0').
            Do not include the word 'licence' in your answer.
            """);

        ChatMessage userPrompt = new(ChatRole.User, licenseContent);

        IChatClient client = _clientLazy.Value;
        
        ChatResponse response = await client.GetResponseAsync([systemPrompt, userPrompt], new ChatOptions
        {
            Temperature = 0.6f
        }).ConfigureAwait(false);
    
#if DEBUG
        for (int index = 0; index < response.Messages.Count; index++)
        {
            ChatMessage msg = response.Messages[index];
            await Console.Out.WriteLineAsync($"Message Number {index + 1} Role: {msg.Role} Text: {msg.Text}").ConfigureAwait(false);
        }
        await Console.Out.WriteLineAsync("").ConfigureAwait(false);
#endif
    
        string? message = response.Messages
            .FirstOrDefault(m => m.Role == ChatRole.Assistant)?.Text;
    
        string? trimmed = message?.Trim();

        if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(trimmed))
            return null;
        
#if DEBUG
        await Console.Out.WriteLineAsync($"RAW Detected: {message}").ConfigureAwait(false);
        await Console.Out.WriteLineAsync($"Trimmed License: {trimmed}").ConfigureAwait(false);
#endif
        
        return trimmed;
    }
}