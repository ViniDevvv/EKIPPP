using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace EKIPPP.Services;

public enum LicenseResult { Valid, Activated, HwidMismatch, Invalid, NetworkError }

public class LicenseService
{
    private const string SupabaseUrl     = "https://wrbvzqhnjkzthguvduvq.supabase.co";
    private const string SupabaseAnonKey = "sb_publishable_ICroIjLqZJB6W8Hk8bo9RA_dxSU3VnN";

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    private static string LicensePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "EKIPPP", "license.dat");

    public bool HasLocalLicense() => File.Exists(LicensePath);

    public string? GetLocalKey()
    {
        try { return File.Exists(LicensePath) ? File.ReadAllText(LicensePath).Trim() : null; }
        catch { return null; }
    }

    public async Task<LicenseResult> ValidateAsync(string key)
    {
        try
        {
            var hwid = GetHWID();
            var body = JsonSerializer.Serialize(new { p_key = key.Trim().ToUpper(), p_hwid = hwid });

            using var req = new HttpRequestMessage(HttpMethod.Post,
                $"{SupabaseUrl}/rest/v1/rpc/activate_license")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("apikey", SupabaseAnonKey);
            req.Headers.Add("Authorization", $"Bearer {SupabaseAnonKey}");

            var resp   = await _http.SendAsync(req);
            var result = (await resp.Content.ReadAsStringAsync()).Trim('"', ' ', '\n', '\r');

            return result switch
            {
                "ACTIVATED"     => LicenseResult.Activated,
                "VALID"         => LicenseResult.Valid,
                "HWID_MISMATCH" => LicenseResult.HwidMismatch,
                _               => LicenseResult.Invalid
            };
        }
        catch { return LicenseResult.NetworkError; }
    }

    public void SaveLicense(string key)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LicensePath)!);
        File.WriteAllText(LicensePath, key.Trim().ToUpper());
    }

    public static string GetHWID()
    {
        using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
        var guid = key?.GetValue("MachineGuid")?.ToString() ?? Environment.MachineName;
        using var sha = System.Security.Cryptography.SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(guid));
        return Convert.ToHexString(hash)[..16];
    }
}
