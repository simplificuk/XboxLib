using System;
using System.Xml;
using System.Xml.Serialization;

namespace XboxLib.Marketplace.GameFeed;

[XmlRoot(ElementName = "media", Namespace = Client.LiveNamespace)]
public class Media
{
    [XmlElement(ElementName = "mediaType", Namespace = Client.LiveNamespace)]
    public uint MediaType { get; set; }

    [XmlElement(ElementName = "gameTitleMediaId", Namespace = Client.LiveNamespace)]
    public string GameTitleMediaId { get; set; }

    [XmlElement(ElementName = "reducedTitle", Namespace = Client.LiveNamespace)]
    public string ReducedTitle { get; set; }

    [XmlElement(ElementName = "reducedDescription", Namespace = Client.LiveNamespace)]
    public string ReducedDescription { get; set; }

    [XmlElement(ElementName = "availabilityDate", Namespace = Client.LiveNamespace)]
    public DateTime AvailabilityDate { get; set; }

    [XmlElement(ElementName = "releaseDate", Namespace = Client.LiveNamespace)]
    public DateTime ReleaseDate { get; set; }

    [XmlElement(ElementName = "ratingId", Namespace = Client.LiveNamespace)]
    public int RatingId { get; set; }

    [XmlElement(ElementName = "customGenre", Namespace = Client.LiveNamespace)]
    public string CustomGenre { get; set; }

    [XmlElement(ElementName = "developer", Namespace = Client.LiveNamespace)]
    public string Developer { get; set; }

    [XmlElement(ElementName = "publisher", Namespace = Client.LiveNamespace)]
    public string Publisher { get; set; }

    [XmlElement(ElementName = "titleId", Namespace = Client.LiveNamespace)]
    public ulong TitleId { get; set; }

    [XmlElement(ElementName = "effectiveTitleId", Namespace = Client.LiveNamespace)]
    public ulong EffectiveTitleId { get; set; }

    [XmlElement(ElementName = "gameReducedTitle", Namespace = Client.LiveNamespace)]
    public string GameReducedTitle { get; set; }

    [XmlElement(ElementName = "fullTitle", Namespace = Client.LiveNamespace)]
    public string FullTitle { get; set; }

    [XmlElement(ElementName = "description", Namespace = Client.LiveNamespace)]
    public string Description { get; set; }

    [XmlElement(ElementName = "gameCapabilities", Namespace = Client.LiveNamespace)]
    public GameCapabilities GameCapabilities { get; set; }

    [XmlElement(ElementName = "ratingAggregate", Namespace = Client.LiveNamespace)]
    public float RatingAggregate { get; set; }

    [XmlElement(ElementName = "numberOfRatings", Namespace = Client.LiveNamespace)]
    public uint NumberOfRatings { get; set; }

    [XmlArray(ElementName = "ratingDescriptors", Namespace = Client.LiveNamespace)]
    public RatingDescriptor[] RatingDescriptors { get; set; }

    [XmlAnyAttribute] public XmlAttribute[] UnknownAttributes { get; set; }
    [XmlAnyElement] public XmlElement[] UnknownElements { get; set; }
}