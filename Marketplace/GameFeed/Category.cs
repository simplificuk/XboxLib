using System.Xml.Serialization;

namespace XboxLib.Marketplace.GameFeed;

[XmlRoot(ElementName = "category", Namespace = Client.LiveNamespace)]
public class Category
{
    [XmlElement(ElementName = "categoryId", Namespace = Client.LiveNamespace)]
    public uint CategoryId { get; set; }

    [XmlElement(ElementName = "system", Namespace = Client.LiveNamespace)]
    public uint System { get; set; }

    [XmlElement(ElementName = "name", Namespace = Client.LiveNamespace)]
    public string Name { get; set; }
}