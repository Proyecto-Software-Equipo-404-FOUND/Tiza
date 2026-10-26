using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PEI.Identidad.UnitTests;

public sealed class Qa02ValidationTests
{
    public static IEnumerable<object[]> ValidationCases()
    {
        var cases=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"qa02-cases.json")));
        return cases.RootElement.EnumerateArray().Where(c=>c.GetProperty("expected").GetInt32()==400)
            .Select(c=>new object[]{c.GetProperty("id").GetString()!,c.GetRawText()}).ToArray();
    }
    [Theory, MemberData(nameof(ValidationCases))]
    public async Task ValidacionNoFiltraSecretosNiPersisteDatos(string id,string json)
    {
        using var fixture=new Qa02Fixture();
        var before=fixture.UserCount;
        var c=JsonDocument.Parse(json).RootElement;
        using var request=new HttpRequestMessage(HttpMethod.Post,c.GetProperty("path").GetString());
        var token=c.GetProperty("token").GetString()!;
        if(token.Length>0)request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",fixture.Variables[token]);
        request.Content=new StringContent(fixture.Expand(c.GetProperty("raw").GetString()!),Encoding.UTF8,"application/json");
        using var response=await fixture.Client.SendAsync(request);
        Assert.True((int)response.StatusCode==400,$"{id}: esperado400, obtenido{(int)response.StatusCode}");
        Assert.Equal("application/problem+json",response.Content.Headers.ContentType?.MediaType);
        var text=await response.Content.ReadAsStringAsync();
        var body=JsonDocument.Parse(text).RootElement;
        Assert.Equal(400,body.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("title").GetString()));
        Assert.NotEmpty(body.GetProperty("errors").EnumerateObject());
        Assert.False(body.TryGetProperty("accessToken",out _));
        foreach(var key in new[]{"qa02Password","qa02WrongPassword","qa02ShortPassword","qa02LongPassword"})
            Assert.DoesNotContain(fixture.Variables[key],text);
        Assert.DoesNotContain("stacktrace",text,StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before,fixture.UserCount);
        Assert.Equal(0,fixture.PlatformWrites);
    }
}
