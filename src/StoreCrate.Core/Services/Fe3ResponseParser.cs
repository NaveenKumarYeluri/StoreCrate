using System.Xml;

namespace StoreCrate.Core.Services;

/// <summary>
/// Parses the XML returned by the FE3 <c>SyncUpdates</c> call.
/// The XML uses a default namespace, so elements are matched by local name.
/// </summary>
public static class Fe3ResponseParser
{
    public static IReadOnlyList<Fe3Entry> Parse(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return Array.Empty<Fe3Entry>();
        }

        var document = new XmlDocument { XmlResolver = null };
        document.LoadXml(xml);

        var entries = new List<Fe3Entry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (XmlNode node in document.GetElementsByTagName("UpdateInfo"))
        {
            if (node is not XmlElement updateInfo)
            {
                continue;
            }

            var identity = updateInfo.GetElementsByTagName("UpdateIdentity").OfType<XmlElement>().FirstOrDefault();
            var metadata = updateInfo.GetElementsByTagName("AppxMetadata").OfType<XmlElement>().FirstOrDefault();
            if (identity is null || metadata is null)
            {
                continue;
            }

            var updateId = identity.GetAttribute("UpdateID");
            var revision = identity.GetAttribute("RevisionNumber");
            var moniker = metadata.GetAttribute("PackageMoniker");
            var type = metadata.GetAttribute("PackageType");

            if (string.IsNullOrWhiteSpace(updateId) ||
                string.IsNullOrWhiteSpace(revision) ||
                string.IsNullOrWhiteSpace(moniker) ||
                !seen.Add(moniker))
            {
                continue;
            }

            entries.Add(new Fe3Entry(updateId, revision, moniker, type));
        }

        return entries;
    }
}
