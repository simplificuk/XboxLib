using System.Xml.Serialization;

namespace XboxLib.Marketplace.GameFeed;

[XmlRoot(ElementName = "ratingDescriptor", Namespace = Client.LiveNamespace)]
public class RatingDescriptor
{
    [XmlElement(ElementName = "ratingDescriptorId", Namespace = Client.LiveNamespace)]
    public string RatingDescriptorId { get; set; }

    [XmlElement(ElementName = "ratingDescriptorLevel", Namespace = Client.LiveNamespace)]
    public string RatingDescriptorLevel { get; set; }
}