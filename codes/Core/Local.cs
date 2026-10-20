using System.Net;
using Microsoft.AspNetCore.Http;

namespace HomeServices.Core
{
    public static class Local
    {
        public static IHttpContextAccessor? Accessor { get; set; }

        public static bool Request()
        {
            HttpContext? h = Accessor?.HttpContext;
            if (h == null)
            {
                return false;
            }
            IPAddress? ip = h.Connection.RemoteIpAddress;
            string host = h.Request.Host.Host;
            bool named = host is "localhost" or "127.0.0.1" or "[::1]" or "::1";
            return ip != null && IPAddress.IsLoopback(ip) && named && !h.Request.Headers.ContainsKey("X-Forwarded-For");
        }
    }
}
