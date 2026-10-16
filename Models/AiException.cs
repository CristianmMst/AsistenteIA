using System.Net;

namespace AsistenteIA.Models;

/// <summary>Error controlado del servicio de IA con un mensaje comprensible para el usuario.</summary>
public class AiServiceException(string userMessage, HttpStatusCode? statusCode = null, string? apiCode = null)
    : Exception(userMessage)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;

    public string? ApiCode { get; } = apiCode;
}
