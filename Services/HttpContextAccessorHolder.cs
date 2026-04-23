using Microsoft.AspNetCore.Http;

namespace RagAgentApi.Services;

/// <summary>
/// Minimal holder to access IHttpContextAccessor from non-controller code when needed.
/// </summary>
public static class HttpContextAccessorHolder
{
    public static IHttpContextAccessor? Accessor { get; set; }
}
