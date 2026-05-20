using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Lite.Shop.AppCodes
{
    public static class SessionExtensions
    {
        public static void SetObject<T>(this ISession session, string key, T value)
        {
            session.SetString(key, JsonSerializer.Serialize(value));
        }

        public static T? GetObject<T>(this ISession session, string key)
        {
            var json = session.GetString(key);
            if (json == null) return default;
            return JsonSerializer.Deserialize<T>(json);
        }
    }
}