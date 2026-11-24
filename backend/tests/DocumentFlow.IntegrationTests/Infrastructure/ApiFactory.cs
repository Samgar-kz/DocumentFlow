using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentFlow.Api.Controllers;
using DocumentFlow.Application.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace DocumentFlow.IntegrationTests.Infrastructure;

/// <summary>Runs the real API against PostgreSQL (Testcontainers, or an existing server) and a temp file store.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string ExternalDatabaseVariable = "DOCUMENTFLOW_TEST_DATABASE";
    public const string AdminEmail = "admin@documentflow.dev";
    public const string DemoEmail = "demo@documentflow.dev";
    public const string Password = "Demo123!";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static readonly byte[] SamplePdf = Encoding.ASCII.GetBytes("%PDF-1.4\n% integration test file\n");

    private readonly string? _externalServer = Environment.GetEnvironmentVariable(ExternalDatabaseVariable);
    private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), $"documentflow-tests-{Guid.NewGuid():N}");
    private PostgreSqlContainer? _container;
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        if (!string.IsNullOrWhiteSpace(_externalServer))
        {
            _connectionString = new NpgsqlConnectionStringBuilder(_externalServer) { Database = $"documentflow_tests_{Guid.NewGuid():N}" }.ConnectionString;
            return;
        }

        _container = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
            return;
        }

        var database = new NpgsqlConnectionStringBuilder(_connectionString).Database;
        await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(_externalServer) { Database = "postgres" }.ConnectionString);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
    }

    public async Task<(HttpClient Client, UserDto User)> CreateUserAsync(string name = "Test User")
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest($"user-{Guid.NewGuid():N}@documentflow.test", "Sup3rSecret!", name, "Testing"),
            Json);
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth.User);
    }

    public async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), Json);
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    public static MultipartFormDataContent DocumentForm(string title, byte[]? bytes = null, string fileName = "contract.pdf", string category = "Contract")
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(title), "title" },
            { new StringContent(category), "category" },
            { new StringContent("Created by an integration test"), "description" },
        };
        var file = new ByteArrayContent(bytes ?? SamplePdf);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        return form;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", _connectionString);
        builder.UseSetting("Jwt:SigningKey", "integration-test-signing-key-0123456789abcdef");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Seed:Enabled", "true");
        builder.UseSetting("Seed:DemoPassword", Password);
        builder.UseSetting("Seed:AdminPassword", Password);
        builder.UseSetting("Storage:RootPath", _storageRoot);
        builder.UseSetting("RateLimiting:AuthPermitLimit", "10000");
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}

internal static class HttpExtensions
{
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"Expected success but got {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<T>(body, ApiFactory.Json)!;
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, ApiFactory.Json);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PutAsJsonAsync(url, body, ApiFactory.Json);
}
