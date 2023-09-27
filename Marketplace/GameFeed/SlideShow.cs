using System.Collections.Generic;
using System.Xml.Serialization;

namespace XboxLib.Marketplace.GameFeed;

[XmlRoot(ElementName = "slideShow", Namespace = Client.LiveNamespace)]
public class SlideShow
{
    [XmlElement(ElementName = "image", Namespace = Client.LiveNamespace)]
    public List<Image> Image { get; set; }
}