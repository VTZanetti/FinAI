namespace FinAI.Api.Common;

/// <summary>
/// Códigos de erro de domínio usados nos resultados dos serviços.
/// </summary>
public enum ErrorCode
{
    None = 0,
    Validation = 1,
    NotFound = 2,
    Conflict = 3,
    BusinessRule = 4,
    Unauthorized = 5,
    Forbidden = 6,
    Internal = 7
}
