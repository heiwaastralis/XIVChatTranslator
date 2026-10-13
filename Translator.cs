using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace FrenchChatTranslator;

public sealed record TranslationResult(string SourceLang, string Text);

/// <summary>
/// Détection automatique de la langue + traduction.
/// Les deux services utilisés détectent eux-mêmes la langue source : on compare
/// ensuite la langue détectée à la langue cible pour décider d'afficher ou non.
/// </summary>
public sealed class Translator : IDisposable
{
    // Timeout court : en mode « remplacer », un message attend la réponse avant d'apparaître.
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(4) };
    private readonly SemaphoreSlim gate = new(3); // max 3 requêtes simultanées
    private readonly Dictionary<string, TranslationResult> cache = new();
    private readonly Queue<string> cacheOrder = new();
    private readonly object cacheLock = new();
    private const int CacheMax = 500;

    /// <summary>Renvoie le résultat s'il est déjà en cache (réponse immédiate, sans réseau).</summary>
    public bool TryGetCached(string text, Configuration cfg, out TranslationResult? result)
    {
        var key = $"{cfg.Provider}|{cfg.TargetLanguage}|{text}";
        lock (this.cacheLock)
        {
            return this.cache.TryGetValue(key, out result);
        }
    }

    // DeepL n'accepte pas tous les codes tels quels (ex: EN seul est déprécié).
    private static string DeepLTarget(string code) => code switch
    {
        "en" => "EN-US",
        "pt" => "PT-BR",
        "zh-CN" => "ZH",
        _ => code.ToUpperInvariant(),
    };

    public async Task<TranslationResult?> TranslateAsync(string text, Configuration cfg, CancellationToken ct)
    {
        var key = $"{cfg.Provider}|{cfg.TargetLanguage}|{text}";
        lock (this.cacheLock)
        {
            if (this.cache.TryGetValue(key, out var hit))
                return hit;
        }

        await this.gate.WaitAsync(ct);
        try
        {
            var result = cfg.Provider == Provider.DeepLFree
                ? await this.DeepLAsync(text, cfg, ct)
                : await this.GoogleAsync(text, cfg, ct);

            if (result != null)
            {
                lock (this.cacheLock)
                {
                    this.cache[key] = result;
                    this.cacheOrder.Enqueue(key);
                    while (this.cacheOrder.Count > CacheMax)
                        this.cache.Remove(this.cacheOrder.Dequeue());
                }
            }

            return result;
        }
        finally
        {
            this.gate.Release();
        }
    }

    // Endpoint public non officiel de Google Translate (sl=auto => détection automatique).
    private async Task<TranslationResult?> GoogleAsync(string text, Configuration cfg, CancellationToken ct)
    {
        var url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&dt=t&tl="
                  + Uri.EscapeDataString(cfg.TargetLanguage)
                  + "&q=" + Uri.EscapeDataString(text);

        using var resp = await this.http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode)
            return null;

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var sb = new StringBuilder();
        foreach (var seg in root[0].EnumerateArray())
        {
            if (seg.GetArrayLength() > 0 && seg[0].ValueKind == JsonValueKind.String)
                sb.Append(seg[0].GetString());
        }

        var lang = root.GetArrayLength() > 2 && root[2].ValueKind == JsonValueKind.String
            ? root[2].GetString() ?? "?"
            : "?";

        return new TranslationResult(lang.ToLowerInvariant(), sb.ToString());
    }

    // DeepL API Free : nécessite une clé gratuite (https://www.deepl.com/pro-api).
    private async Task<TranslationResult?> DeepLAsync(string text, Configuration cfg, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cfg.DeepLApiKey))
            return null;

        // Les clés du plan gratuit finissent par ":fx" et utilisent un autre serveur que les clés payantes.
        var apiKey = cfg.DeepLApiKey.Trim();
        var host = apiKey.EndsWith(":fx", StringComparison.Ordinal) ? "api-free.deepl.com" : "api.deepl.com";

        using var req = new HttpRequestMessage(HttpMethod.Post, $"https://{host}/v2/translate");
        req.Headers.TryAddWithoutValidation("Authorization", "DeepL-Auth-Key " + apiKey);
        req.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("text", text),
            new KeyValuePair<string, string>("target_lang", DeepLTarget(cfg.TargetLanguage)),
        });

        using var resp = await this.http.SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
            return null;

        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var t = doc.RootElement.GetProperty("translations")[0];
        var lang = t.GetProperty("detected_source_language").GetString() ?? "?";
        var translated = t.GetProperty("text").GetString() ?? string.Empty;
        return new TranslationResult(lang.ToLowerInvariant(), translated);
    }

    public void Dispose()
    {
        this.http.Dispose();
        this.gate.Dispose();
    }
}
