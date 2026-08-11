namespace FinAI.Api.Security;

/// <summary>
/// Provedor do usuário autenticado na requisição atual.
/// Na v0.1 retorna o usuário de desenvolvimento; na v0.2 passa a ler o claim <c>sub</c> do JWT.
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }
}

/// <summary>
/// Implementação de desenvolvimento (v0.1) — lê o <c>DevUser:Id</c> da configuração.
/// A autenticação real (claims JWT) chega na v0.2.
/// </summary>
public sealed class DevCurrentUser : ICurrentUser
{
    private readonly Guid _userId;

    public DevCurrentUser(IConfiguration configuration)
    {
        var raw = configuration["DevUser:Id"];
        _userId = Guid.TryParse(raw, out var id) ? id : Guid.Empty;
    }

    public Guid UserId => _userId;
}
