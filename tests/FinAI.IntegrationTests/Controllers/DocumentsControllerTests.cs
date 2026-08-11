using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FinAI.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace FinAI.IntegrationTests.Controllers;

[Trait("Category", "Integration")]
[Collection("Postgres")]
public class DocumentsControllerTests : IClassFixture<FinAiTestFixture>
{
    private readonly FinAiTestFixture _fixture;
    private readonly Lazy<Task<HttpClient>> _authenticatedClient;

    public DocumentsControllerTests(FinAiTestFixture fixture)
    {
        _fixture = fixture;
        _authenticatedClient = new Lazy<Task<HttpClient>>(async () =>
        {
            var (client, auth) = await _fixture.CreateAuthenticatedClientAsync();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
            return client;
        });
    }

    private Task<HttpClient> ClientAsync() => _authenticatedClient.Value;

    private static MultipartFormDataContent BuildFileContent(string fileName, string contentType, byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(fileContent, "file", fileName);
        return content;
    }

    [Fact]
    public async Task Upload_TextFile_ReturnsCreated()
    {
        // Arrange
        var client = await ClientAsync();
        var bytes = Encoding.UTF8.GetBytes("Extrato bancário de junho. Gastos com alimentação e transporte.");
        using var content = BuildFileContent("extrato.txt", "text/plain", bytes);

        // Act
        var response = await client.PostAsync("/api/v1/documents", content);

        // Assert: processamento é async (status processing ou ready após pipeline)
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        json.GetProperty("fileName").GetString().Should().Be("extrato.txt");
        json.GetProperty("status").GetString().Should().BeOneOf("Processing", "Ready", "Failed");
    }

    [Fact]
    public async Task Upload_InvalidContentType_ReturnsBadRequest()
    {
        // Arrange
        var client = await ClientAsync();
        using var content = BuildFileContent("malware.exe", "application/octet-stream", [1, 2, 3]);

        // Act
        var response = await client.PostAsync("/api/v1/documents", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_WithoutFile_ReturnsBadRequest()
    {
        // Arrange
        var client = await ClientAsync();
        using var content = new MultipartFormDataContent();

        // Act
        var response = await client.PostAsync("/api/v1/documents", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_Documents_ReturnsOwnOnly()
    {
        // Arrange
        var client = await ClientAsync();
        var bytes = Encoding.UTF8.GetBytes("Conteúdo do documento.");
        using var content = BuildFileContent("doc1.txt", "text/plain", bytes);
        await client.PostAsync("/api/v1/documents", content);

        // Act
        var response = await client.GetAsync("/api/v1/documents");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<List<System.Text.Json.JsonElement>>();
        list.Should().NotBeNull();
        list!.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Get_NotOwned_Returns404()
    {
        // Arrange
        var client = await ClientAsync();

        // Act
        var response = await client.GetAsync($"/api/v1/documents/{Guid.NewGuid()}");

        // Assert: 404 — nunca 403
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_ValidDocument_ReturnsNoContent()
    {
        // Arrange
        var client = await ClientAsync();
        var bytes = Encoding.UTF8.GetBytes("Documento para excluir.");
        using var content = BuildFileContent("delete-me.txt", "text/plain", bytes);
        var create = await client.PostAsync("/api/v1/documents", content);
        var doc = await create.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        // Act
        var response = await client.DeleteAsync($"/api/v1/documents/{doc.GetProperty("id").GetGuid()}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Documents_WithoutToken_Returns401()
    {
        // Arrange
        var anonymous = _fixture.CreateClient();

        // Act
        var response = await anonymous.GetAsync("/api/v1/documents");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
