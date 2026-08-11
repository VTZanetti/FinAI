namespace FinAI.Api.Security;

public static class CurrentUserExtensions
{
    /// <summary>
    /// Obtém o UserId obrigatório do usuário autenticado.
    /// Só deve ser chamado em endpoints com [Authorize] — o claim <c>sub</c> sempre existe.
    /// </summary>
    public static Guid RequireUserId(this ICurrentUser currentUser)
        => currentUser.UserId ?? throw new InvalidOperationException("User is not authenticated");
}
