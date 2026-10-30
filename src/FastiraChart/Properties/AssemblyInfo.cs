using System.Windows;
using System.Windows.Markup;

// Directs WPF to locate the default control theme (Themes/Generic.xaml) in this assembly.
[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,
    ResourceDictionaryLocation.SourceAssembly)]

// One XAML namespace for every public type, whichever CLR namespace it is declared in:
// xmlns:fc="urn:fastirachart:wpf". The four CLR namespaces below are every one that holds an exported type; the type
// names across them are distinct, so no name is ambiguous in XAML.
[assembly: XmlnsDefinition("urn:fastirachart:wpf", "FastiraChart")]
[assembly: XmlnsDefinition("urn:fastirachart:wpf", "FastiraChart.Axes")]
[assembly: XmlnsDefinition("urn:fastirachart:wpf", "FastiraChart.ReferenceLines")]
[assembly: XmlnsDefinition("urn:fastirachart:wpf", "FastiraChart.Series")]
[assembly: XmlnsPrefix("urn:fastirachart:wpf", "fc")]
