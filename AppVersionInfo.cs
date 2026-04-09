using System.Reflection;

namespace TabletMidiPad;

internal static class AppVersionInfo
{
    private static readonly Lazy<string> InformationalVersion = new(ResolveInformationalVersion);

    internal static string InformationalVersionOrFallback => InformationalVersion.Value;

    private static string ResolveInformationalVersion()
    {
        var asm = Assembly.GetEntryAssembly();
        var attr = asm?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(attr))
        {
            return asm?.GetName().Version?.ToString(3) ?? "0.0.0";
        }

        var plus = attr.IndexOf('+', StringComparison.Ordinal);
        return plus >= 0 ? attr[..plus] : attr;
    }
}
