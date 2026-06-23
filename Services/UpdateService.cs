using System.Net.Http;
using System.Text.Json;

namespace EKIPPP.Services;

public record UpdateInfo(string Version, string DownloadUrl, string Changelog);

public static class UpdateService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };

    public static async Task<UpdateInfo?> CheckAsync(string url)
    {
        try
        {
            var json = (await _http.GetStringAsync(url)).TrimStart('﻿');
            var doc  = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new UpdateInfo(
                root.GetProperty("version").GetString()      ?? "0",
                root.GetProperty("download_url").GetString() ?? "",
                root.GetProperty("changelog").GetString()    ?? "");
        }
        catch { return null; }
    }
}
