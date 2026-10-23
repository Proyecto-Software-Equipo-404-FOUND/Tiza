using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PEI.Identidad.Application.Abstractions;
using PEI.Identidad.Application.Interfaces.Security;
using PEI.Identidad.Application.UseCases.Auth.Register;
using PEI.Identidad.Domain.Entities;

namespace PEI.Identidad.UnitTests;

public sealed class RegistrationCancellationTests
{
    private sealed record Tenant(Guid? InstitucionId) : ITenantContext;
    private sealed class Hasher(Action? hash = null) : IPasswordHasher
    {
        public bool Called { get; private set; }
        public string Hash(string password) { Called = true; hash?.Invoke(); return "hash-sintetico"; }
        public bool Verify(string password, string hash) => throw new NotSupportedException();
        public void VerifyDummy(string password) => throw new NotSupportedException();
    }
    private sealed class Data : IRegistrationDataAccess
    {
        public List<(string Step, CancellationToken Token)> Calls { get; } = new();
        public Action<string>? OnCall { get; set; }
        public Func<CancellationToken, Task<bool>>? Permission { get; set; }
        public Usuario? Added { get; private set; }
        public bool Saved { get; private set; }
        private void Check(string step, CancellationToken token)
        {
            Calls.Add((step, token)); token.ThrowIfCancellationRequested();
            OnCall?.Invoke(step); token.ThrowIfCancellationRequested();
        }
        public Task<bool> PuedeRegistrarAsync(Guid id, CancellationToken ct = default)
        { Check("autorizacion", ct); return Permission?.Invoke(ct) ?? Task.FromResult(true); }
        public Task<bool> ExisteEmailAsync(string email, CancellationToken ct = default)
        { Check("correo", ct); return Task.FromResult(false); }
        public Task AddAsync(Usuario user, CancellationToken ct = default)
        { Check("agregar", ct); Added = user; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct = default)
        { Check("guardar", ct); Saved = true; return Task.CompletedTask; }
    }
    private static RegisterCommand Command() => new()
    { Email = "h3@test.invalid", Password = "Sintetica123!", Nombre = "Prueba", Apellido = "H3", Rol = "Alumno" };

    [Fact]
    public async Task ApplicationPropagaMismoTokenYRegistroNormalConservaResultado()
    {
        using var cancellation = new CancellationTokenSource();
        var data = new Data(); var tenant = new Tenant(Guid.NewGuid());
        var id = await new RegisterHandler(data, new Hasher(), tenant).HandleAsync(Command(), cancellation.Token);
        Assert.Equal(new[] { "autorizacion", "correo", "agregar", "guardar" }, data.Calls.Select(c => c.Step));
        Assert.All(data.Calls, c => Assert.Equal(cancellation.Token, c.Token));
        Assert.True(data.Saved); Assert.Equal(data.Added!.Id, id);
        Assert.Equal(tenant.InstitucionId, data.Added.InstitucionId);
    }

    [Theory]
    [InlineData("inicio")]
    [InlineData("autorizacion")]
    [InlineData("correo")]
    [InlineData("hash")]
    [InlineData("agregar")]
    [InlineData("guardar")]
    public async Task CancelacionAnteriorAGuardarNoConfirmaYHashNoContinuaElFlujo(string stage)
    {
        using var cancellation = new CancellationTokenSource();
        var data = new Data { OnCall = step => { if (step == stage) cancellation.Cancel(); } };
        var hasher = new Hasher(() => { if (stage == "hash") cancellation.Cancel(); });
        if (stage == "inicio") cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new RegisterHandler(data, hasher, new Tenant(Guid.NewGuid())).HandleAsync(Command(), cancellation.Token));
        Assert.False(data.Saved);
        Assert.All(data.Calls, c => Assert.Equal(cancellation.Token, c.Token));
        if (stage is "inicio" or "autorizacion" or "correo") Assert.False(hasher.Called);
        if (stage == "inicio") Assert.Empty(data.Calls);
        if (stage == "hash") Assert.DoesNotContain(data.Calls, c => c.Step == "agregar");
    }

    [Fact]
    public async Task EndpointPropagaRequestAbortedSinAlterar201()
    {
        using var factory = new IdentityHttpFactory();
        var data = new Data(); CancellationToken actual = default;
        using var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IRegistrationDataAccess>();
            s.AddScoped<IRegistrationDataAccess>(sp =>
            {
                actual = sp.GetRequiredService<IHttpContextAccessor>().HttpContext!.RequestAborted;
                return data;
            });
        }));
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token());
        var response = await client.PostAsJsonAsync("/api/auth/register", Command());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(actual.CanBeCanceled); Assert.Equal(4, data.Calls.Count);
        Assert.All(data.Calls, c => Assert.Equal(actual, c.Token));
        Assert.True(data.Saved);
    }

    [Fact]
    public async Task CancellationTokenNoAgregaParametrosAlContratoOpenApi()
    {
        using var factory = new IdentityHttpFactory(hostEnvironment: "Development");
        using var client = factory.Client();
        var document = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/swagger/v1/swagger.json");
        var operation = document.GetProperty("paths").GetProperty("/api/auth/register").GetProperty("post");
        if (operation.TryGetProperty("parameters", out var parameters))
            Assert.DoesNotContain(parameters.EnumerateArray(), p => p.GetProperty("name").GetString() is "ct" or "cancellationToken");
        Assert.Equal("#/components/schemas/RegisterCommand", operation.GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString());
    }

    [Fact]
    public async Task CancelarPeticionCancelaDependenciaSinLlegarAPersistencia()
    {
        using var factory = new IdentityHttpFactory();
        var entered = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var observed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var data = new Data
        {
            Permission = async token =>
            {
                entered.TrySetResult(token);
                try { await new TaskCompletionSource<bool>().Task.WaitAsync(token); return true; }
                catch (OperationCanceledException) { observed.TrySetResult(true); throw; }
            }
        };
        using var host = factory.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IRegistrationDataAccess>(); s.AddSingleton<IRegistrationDataAccess>(data);
        }));
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        client.DefaultRequestHeaders.Authorization = new("Bearer", factory.Token());
        using var cancellation = new CancellationTokenSource();
        var request = client.PostAsJsonAsync("/api/auth/register", Command(), cancellation.Token);
        var actual = await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        Assert.True(await observed.Task.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(actual.IsCancellationRequested);
        Assert.Single(data.Calls); Assert.Null(data.Added); Assert.False(data.Saved);
    }
}
