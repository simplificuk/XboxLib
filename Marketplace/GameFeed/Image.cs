using System.Xml.Serialization;

namespace XboxLib.Marketplace.GameFeed;

[XmlRoot(ElementName = "image", Namespace = Client.LiveNamespace)]
public class Image
{
    [XmlElement(ElementName = "imageMediaId", Namespace = Client.LiveNamespace)]
    public string ImageMediaId { get; set; }

    [XmlElement(ElementName = "imageMediaInstanceId", Namespace = Client.LiveNamespace)]
    public string ImageMediaInstanceId { get; set; }

    [XmlElement(ElementName = "imageMediaType", Namespace = Client.LiveNamespace)]
    public uint ImageMediaType { get; set; }

    [XmlElement(ElementName = "relationshipType", Namespace = Client.LiveNamespace)]
    public uint RelationshipType { get; set; }

    [XmlElement(ElementName = "format", Namespace = Client.LiveNamespace)]
    public uint Format { get; set; }

    [XmlElement(ElementName = "size", Namespace = Client.LiveNamespace)]
    public uint Size { get; set; }

    [XmlElement(ElementName = "fileUrl", Namespace = Client.LiveNamespace)]
    public string FileUrl { get; set; }
}