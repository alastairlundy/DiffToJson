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

namespace DiffToJsonLib;

public class ChatClientFactory : IChatClientFactory
{
    private readonly string _provider;
    private readonly string _apiKey;
    private readonly string _endpoint;
    private readonly string _model;
    private IChatClient? _cachedClient;
    private readonly Lock _lock = new();

    public ChatClientFactory(string provider, string apiKey, string endpoint, string model)
    {
        _provider = provider;
        _apiKey = apiKey;
        _endpoint = endpoint;
        _model = model;
    }

    public IChatClient Create()
    {
        if (_cachedClient is not null)
            return _cachedClient;

        lock (_lock)
        {
            if (_cachedClient is not null)
                return _cachedClient;

            _cachedClient = BuildClient();
            return _cachedClient;
        }
    }

    private IChatClient BuildClient()
    {
        ArgumentException.ThrowIfNullOrEmpty(_model);

        if (string.IsNullOrEmpty(_provider))
            ArgumentException.ThrowIfNullOrEmpty(_apiKey);

        switch (_provider.ToLower())
        {
            case "anthropic-compatible":
            {
                ArgumentException.ThrowIfNullOrEmpty(_endpoint);

                return new AnthropicClient(new ClientOptions
                    {
                        ApiKey = _apiKey,
                        BaseUrl = _endpoint
                    })
                    .AsIChatClient(_model);
            }
            case "anthropic":
            {
                return new AnthropicClient(new ClientOptions
                    {
                        ApiKey = _apiKey,
                    })
                    .AsIChatClient(_model);
            }
            case "ollama-cloud":
            {
                HttpClient httpClient = new();
                httpClient.BaseAddress = new Uri("https://ollama.com");
                httpClient.DefaultRequestHeaders.Add("Authorization: Bearer", _apiKey);

                return new OllamaApiClient(httpClient, _model, OllamaJsonContext.Default);
            }
            case "ollama":
            {
                ArgumentException.ThrowIfNullOrEmpty(_endpoint);

                OllamaApiClient.Configuration configuration = new()
                {
                    JsonSerializerContext = OllamaJsonContext.Default,
                    Model = _model,
                    Uri = new Uri(_endpoint)
                };
                return new OllamaApiClient(configuration);
            }
            case "openai":
            {
                return new OpenAIClient(new ApiKeyCredential(_apiKey))
                    .GetChatClient(_model)
                    .AsIChatClient();
            }
            // ReSharper disable once RedundantCaseLabel
            case "openai-compatible":
            default:
            {
                ArgumentException.ThrowIfNullOrEmpty(_endpoint);
                return new OpenAIClient(new ApiKeyCredential(_apiKey), new OpenAIClientOptions
                    {
                        Endpoint = new Uri(_endpoint)
                    })
                    .GetChatClient(_model)
                    .AsIChatClient();
            }
        }
    }
}
