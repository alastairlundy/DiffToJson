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
using Anthropic;
using Anthropic.Core;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;

namespace DiffToJsonCli.Helpers;

internal static class ChatClientCreator
{
    internal static IChatClient CreateClient(string provider, string apiKey, string endpoint, string model)
    {
        IChatClient client;
        
        ArgumentException.ThrowIfNullOrEmpty(endpoint);
        ArgumentException.ThrowIfNullOrEmpty(model);
        
        if(string.IsNullOrEmpty(provider))
            ArgumentException.ThrowIfNullOrEmpty(apiKey);
        
        switch (provider.ToLower())
        {
            case "anthropic":
            {
                client = new AnthropicClient(new ClientOptions
                    {
                        ApiKey = apiKey,
                        BaseUrl = endpoint
                    })
                    .AsIChatClient(model);
                break;
            }
            case "ollama-cloud":
            {
                HttpClient httpClient = new();
                httpClient.BaseAddress = new Uri("https://ollama.com");
                httpClient.DefaultRequestHeaders.Add("Authorization: Bearer", apiKey);
                
                client = new OllamaApiClient(httpClient, model, CustomOllamaJsonContext.Default);

                break;
            }
            case "ollama":
            {
                OllamaApiClient.Configuration configuration = new OllamaApiClient.Configuration
                {
                    JsonSerializerContext = CustomOllamaJsonContext.Default,
                    Model = model,
                    Uri = new Uri(endpoint)
                };
                client = new OllamaApiClient(configuration);

                break;
            }
            default:
            {
                client = new OpenAIClient(new ApiKeyCredential(apiKey), new OpenAIClientOptions
                    {
                        Endpoint = new  Uri(endpoint)
                    }) .GetChatClient(model)
                    .AsIChatClient();
                
                break;
            }
        }

        return client;
    }
}