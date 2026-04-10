namespace TabletMidiPad.Services.Naming;

/// <summary>
/// Generates stable default names using the first available one-based index.
/// </summary>
internal static class DefaultNameGenerator
{
    public static string GetNext(IEnumerable<string?> existingNames, string prefix)
    {
        ArgumentNullException.ThrowIfNull(existingNames);
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("Prefix is required.", nameof(prefix));
        }

        var used = new HashSet<int>();
        string normalizedPrefix = prefix.Trim();
        string expectedPrefix = normalizedPrefix + " ";

        foreach (string? raw in existingNames)
        {
            string name = (raw ?? string.Empty).Trim();
            if (!name.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string suffix = name[expectedPrefix.Length..];
            if (int.TryParse(suffix, out int number) && number > 0)
            {
                used.Add(number);
            }
        }

        int next = 1;
        while (used.Contains(next))
        {
            next++;
        }

        return $"{normalizedPrefix} {next}";
    }
}
