using System.Reflection;

namespace TraderUp;

public static class Utils
{
    public static string GetVersion(this Assembly? assembly, bool includePrefix = true)
    {
        const string prefix = "v";
        string? version = assembly?.GetName()?.Version?.ToString(2);

        if (version is null)
        {
            return "unknown version";
        }

        if (includePrefix && !version.StartsWith(prefix))
        {
            version = prefix + version;
        }
            
        return version;
    }
}
