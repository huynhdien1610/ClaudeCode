using System.Text.Json;
using System.Text.Json.Nodes;

namespace Anima.IntegrationTests;

/// <summary>
/// Cổng hợp đồng API (T012): tài liệu OpenAPI sinh từ code phải khớp snapshot contracts/openapi/anima.v1.json.
/// Đổi API là có chủ ý: chạy lại với UPDATE_OPENAPI=1 để cập nhật snapshot và commit cùng thay đổi.
/// </summary>
public sealed class OpenApiContractTests(ApiFixture fx) : IClassFixture<ApiFixture>
{
    private static string SnapshotPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "contracts"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Cannot find repo root"), "contracts", "openapi", "anima.v1.json");
    }

    private async Task<string> Generate()
    {
        using var http = fx.CreateClient();
        var raw = await http.GetStringAsync("/openapi/v1.json");
        var node = JsonNode.Parse(raw)!;
        // Endpoint vận hành và endpoint chỉ có ở dev không thuộc hợp đồng công khai.
        var paths = node["paths"]!.AsObject();
        foreach (var k in paths.Select(p => p.Key).Where(k => !k.StartsWith("/v1/", StringComparison.Ordinal) || k.StartsWith("/v1/dev/", StringComparison.Ordinal)).ToList()) paths.Remove(k);
        return node.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n";
    }

    [Fact(DisplayName = "T012: OpenAPI sinh từ code khớp snapshot hợp đồng")]
    public async Task SnapshotMatches()
    {
        var current = await Generate();
        var path = SnapshotPath();
        if (Environment.GetEnvironmentVariable("UPDATE_OPENAPI") == "1") { await File.WriteAllTextAsync(path, current); return; }
        Assert.True(File.Exists(path), "Thiếu snapshot. Chạy: UPDATE_OPENAPI=1 dotnet test backend/tests/Anima.IntegrationTests --filter OpenApiContract");
        var expected = await File.ReadAllTextAsync(path);
        Assert.True(expected == current, "Hợp đồng API đã thay đổi. Nếu có chủ ý: UPDATE_OPENAPI=1 dotnet test backend/tests/Anima.IntegrationTests --filter OpenApiContract rồi commit snapshot.");
    }

    [Fact(DisplayName = "Mọi endpoint nghiệp vụ nằm dưới /v1 và có trong hợp đồng")]
    public async Task AllBusinessEndpointsAreVersioned()
    {
        var doc = JsonNode.Parse(await Generate())!;
        var paths = doc["paths"]!.AsObject().Select(p => p.Key).ToList();
        Assert.All(paths, p => Assert.StartsWith("/v1/", p));
        foreach (var expected in new[] { "/v1/accounts", "/v1/auth/login", "/v1/wallet", "/v1/wallet/convert", "/v1/packs/{code}/purchase", "/v1/pack-instances/{id}/open", "/v1/forge", "/v1/fairness/verify", "/v1/collection" })
            Assert.Contains(expected, paths);
    }
}
