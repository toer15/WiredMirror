using System.Globalization;
using System.Reflection;
using System.Xml.Linq;

namespace IPhoneMirror.DriverInstaller.Services;

// Reuse the upstream English messages without bringing its WPF UI into the helper.
internal static class DriverLocalization
{
    private static readonly Lazy<Dictionary<string, string>> Strings = new(() =>
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Setup.Strings.xml")!;
        return XDocument.Load(stream).Descendants().Where(element => element.Name.LocalName == "String")
            .ToDictionary(element => element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))!.Value,
                element => element.Value);
    });
    internal static string Get(string key) => Strings.Value.GetValueOrDefault(key, key);
    internal static string Format(string key, params object?[] args) => string.Format(CultureInfo.GetCultureInfo("en-US"), Get(key), args);
}
