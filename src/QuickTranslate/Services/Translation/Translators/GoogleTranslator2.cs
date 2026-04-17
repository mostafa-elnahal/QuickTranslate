using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using GTranslate.Models;
using GTranslate.Results;
using GTranslate;
using QuickTranslate.Models;
using GTranslate.Translators;

namespace QuickTranslate.Services.Translators;

/// <summary>
/// A custom implementation of the Google Translate RPC API (v2 behavior).
/// Note: This version currently does not return rich dictionary data.
/// </summary>
public sealed class GoogleTranslator2 : ITranslator, IDisposable
{
    private const string TranslateRpcId = "MkEWBc";
    private static readonly Uri DefaultBaseAddress = new("https://translate.google.com/");
    private const int MaxTextLength = 5000;

    public string Name => "Google (RPC)";

    private readonly HttpClient _httpClient;
    private bool _disposed;

    public GoogleTranslator2()
    {
        _httpClient = new HttpClient();
        // _httpClient.BaseAddress = DefaultBaseAddress; // We construct absolute or relative URI manually anyway
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/96.0.4664.45 Safari/537.36");
    }

    public async Task<ITranslationResult> TranslateAsync(string text, string toLanguage, string? fromLanguage = null)
    {
        return await TranslateAsync(text, Language.GetLanguage(toLanguage), fromLanguage != null ? Language.GetLanguage(fromLanguage) : null);
    }

    public async Task<ITranslationResult> TranslateAsync(string text, ILanguage toLanguage, ILanguage? fromLanguage = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(toLanguage);

        string fromCode = fromLanguage?.ISO6391 ?? "auto";
        string toCode = toLanguage.ISO6391;

        object[] payload = [new object[] { text, GoogleHotPatch(fromCode), GoogleHotPatch(toCode), 1 }, Array.Empty<object>()];
        using var request = BuildRequest(TranslateRpcId, payload);
        using var document = await SendAndParseResponseAsync(request).ConfigureAwait(false);

        var root = document.RootElement;

        string target = root[1][1].GetString() ?? toCode;
        string source = root[1][3].GetString() ?? "en";

        if (source == "auto")
        {
            source = root.GetArrayLength() > 2 ? root[2].GetString() ?? "en" : "en";
        }

        string translation;
        // Logic copied from library, slightly simplified for safety
        if (root[1][0][0].ValueKind == JsonValueKind.Array && root[1][0][0].GetArrayLength() > 5 && root[1][0][0][5].ValueKind == JsonValueKind.Array)
        {
            translation = string.Join(" ", root[1][0][0][5].EnumerateArray().Select(x => x[0].GetString()));
        }
        else
        {
            translation = root[1][0][0][0].GetString() ?? "";
        }

        string? targetTransliteration = root[1][0][0].GetArrayLength() > 1 ? root[1][0][0][1].GetString() : null;

        // Return result. Note: No rich dictionary data available in this response format currently.
        return new QuickTranslate.Models.GoogleTranslationResult(
            translation,
            text,
            toLanguage as Language ?? Language.GetLanguage(target),
            Language.GetLanguage(source),
            Name,
            new List<DictionaryEntry>(), // Empty dictionary entries
            targetTransliteration
        );
    }

    private static HttpRequestMessage BuildRequest(string rpcId, object?[] payload)
    {
        // Custom serialization to match needed format using basic JsonSerializer
        string serializedPayload = JsonSerializer.Serialize(payload);

        // The RPC format requires nested arrays: [[[rpcId, payload_str, null, "generic"]]]
        object?[][][] request = [[[rpcId, serializedPayload, null, "generic"]]];

        // Serialize the outer request structure
        string fReq = JsonSerializer.Serialize(request);

        return new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri(DefaultBaseAddress, $"_/TranslateWebserverUi/data/batchexecute?rpcids={rpcId}"),
            Content = new FormUrlEncodedContent([new KeyValuePair<string, string>("f.req", fReq)])
        };
    }

    private async Task<JsonDocument> SendAndParseResponseAsync(HttpRequestMessage request)
    {
        using var response = await _httpClient.SendAsync(request).ConfigureAwait(false);
        var content = await response.Content.ReadAsStringAsync();

        // skip magic chars )]}' if present
        int index = content.IndexOf("\n", StringComparison.Ordinal);
        if (index >= 0)
        {
            content = content.Substring(index + 1);
        }

        using var document = JsonDocument.Parse(content);

        // get the actual data: root[0][2] contains the JSON string
        // The response format from batchexecute is a wrapper array.
        if (document.RootElement.ValueKind == JsonValueKind.Array && document.RootElement.GetArrayLength() > 0)
        {
            var inner = document.RootElement[0];
            if (inner.GetArrayLength() > 2)
            {
                string? data = inner[2].GetString();
                if (!string.IsNullOrEmpty(data))
                {
                    return JsonDocument.Parse(data);
                }
            }
        }
        throw new Exception("Invalid RPC response format.");
    }

    private static string GoogleHotPatch(string languageCode)
    {
        return languageCode switch
        {
            "mni" => "mni-Mtei",
            "prs" => "fa-FA",
            "nqo" => "bm-Nkoo",
            "ndc" => "ndc-ZW",
            "sat" => "sat-Latn",
            _ => languageCode
        };
    }

    public Task<ITransliterationResult> TransliterateAsync(string text, string toLanguage, string? fromLanguage = null) => throw new NotImplementedException();
    public Task<ITransliterationResult> TransliterateAsync(string text, ILanguage toLanguage, ILanguage? fromLanguage = null) => throw new NotImplementedException();
    public Task<ILanguage> DetectLanguageAsync(string text) => throw new NotImplementedException();
    public bool IsLanguageSupported(string language) => true;
    public bool IsLanguageSupported(ILanguage language) => true;

    public void Dispose()
    {
        if (!_disposed)
        {
            _httpClient.Dispose();
            _disposed = true;
        }
    }
}
