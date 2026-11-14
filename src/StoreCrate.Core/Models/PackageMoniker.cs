using System.Globalization;

namespace StoreCrate.Core.Models;

/// <summary>
/// Parsed representation of a Store package moniker:
/// <c>Name_Version_Architecture__PublisherId</c>.
/// </summary>
public readonly record struct PackageMoniker(
    string Name,
    string VersionText,
    string Architecture,
    string PublisherId)
{
    public Version? Version => System.Version.TryParse(VersionText, out var version) ? version : null;

    public static PackageMoniker Parse(string moniker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moniker);

        var tokens = moniker.Split('_');

        if (tokens.Length == 1)
        {
            return new PackageMoniker(tokens[0], string.Empty, "neutral", string.Empty);
        }

        if (tokens.Length == 2)
        {
            if (System.Version.TryParse(tokens[1], out _))
            {
                return new PackageMoniker(tokens[0], tokens[1], "neutral", string.Empty);
            }

            return new PackageMoniker(tokens[0], string.Empty, "neutral", tokens[1]);
        }

        if (tokens.Length == 3)
        {
            return new PackageMoniker(tokens[0], tokens[1], tokens[2], string.Empty);
        }

        // When tokens.Length >= 4:
        // Handles:
        // Name_Version_Architecture__PublisherId (tokens.Length == 5, tokens[3] == "")
        // Name_Version_Architecture_~_PublisherId (tokens.Length == 5, tokens[3] == "~")
        // Name_Version_Architecture_ResourceId_PublisherId (tokens.Length == 5, tokens[3] == ResourceId)
        // Name_Version_Architecture_PublisherId (tokens.Length == 4)
        var name = tokens[0];
        var versionText = tokens[1];
        var architecture = tokens[2];
        var publisherId = tokens[^1];

        return new PackageMoniker(name, versionText, architecture, publisherId);
    }

    public override string ToString()
    {
        if (string.IsNullOrWhiteSpace(PublisherId))
        {
            return string.IsNullOrWhiteSpace(VersionText)
                ? Name
                : $"{Name}_{VersionText}_{Architecture}";
        }

        return string.IsNullOrWhiteSpace(VersionText)
            ? $"{Name}_{PublisherId}"
            : $"{Name}_{VersionText}_{Architecture}__{PublisherId}";
    }
}
