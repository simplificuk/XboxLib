using System;
using System.Xml.Serialization;

namespace XboxLib.Marketplace.GameFeed;

[XmlRoot(ElementName = "entry")]
public class Entry
{
    [XmlElement(ElementName = "id")] public string Id { get; set; }
    [XmlElement(ElementName = "updated")] public DateTime Updated { get; set; }
    [XmlElement(ElementName = "title")] public string Title { get; set; }
    [XmlElement(ElementName = "content")] public string Content { get; set; }

    [XmlElement(ElementName = "media", Namespace = Client.LiveNamespace)]
    public Media Media { get; set; }

    [XmlArray("categories", Namespace = Client.LiveNamespace)]
    [XmlArrayItem("category", Type = typeof(Category))]
    public Category[] Categories { get; set; }

    [XmlArray(ElementName = "images", Namespace = Client.LiveNamespace)]
    [XmlArrayItem("image", Type = typeof(Image))]
    public Image[] Images { get; set; }

    [XmlArray(ElementName = "slideShows", Namespace = Client.LiveNamespace)]
    [XmlArrayItem("slideShow", Namespace = Client.LiveNamespace)]
    public SlideShow[] SlideShows { get; set; }
    // [XmlElement(ElementName="previewInstances", Namespace=Client.LiveNamespace)]
    // public PreviewInstances PreviewInstances { get; set; }
}