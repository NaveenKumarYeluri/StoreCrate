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

        var separator = moniker.LastIndexOf("__", StringComparison.Ordinal);
        var left = separator >= 0 ? moniker[..separator] : moniker;
        var publisherId = separator >= 0 ? moniker[(separator + 2)..] : string.Empty;

        var archSeparator = left.LastIndexOf('_');
        if (archSeparator <= 0)
        {
            return new PackageMoniker(left, string.Empty, "neutral", publisherId);
        }

        var architecture = left[(archSeparator + 1)..];
        var beforeArch = left[..archSeparator];

        var nameSeparator = beforeArch.IndexOf('_');
        if (nameSeparator <= 0)
        {
            return new PackageMoniker(beforeArch, string.Empty, architecture, publisherId);
        }

        return new PackageMoniker(
            beforeArch[..nameSeparator],
            beforeArch[(nameSeparator + 1)..],
            architecture,
            publisherId);
    }

    public override string ToString() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{Name}_{VersionText}_{Architecture}__{PublisherId}");
}
