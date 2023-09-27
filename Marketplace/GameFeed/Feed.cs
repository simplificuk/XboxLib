using System;
using System.Xml;
using System.Xml.Serialization;

namespace XboxLib.Marketplace.GameFeed;

[XmlRoot("feed")]
public class Feed
{
    [XmlElement("totalItems", Namespace = Client.LiveNamespace)]
    public uint TotalItems { get; set; }
    [XmlElement("numItems", Namespace = Client.LiveNamespace)]
    public uint NumItems { get; set; }
    [XmlElement("title")]
    public string Title { get; set; }
    [XmlElement("updated")]
    public DateTime Updated { get; set; }
    
    [XmlElement("entry")]
    public Entry[] Entries { get; set; }
    
    [XmlAnyAttribute]
    public XmlAttribute[] UnknownAttributes { get; set; }
    [XmlAnyElement]
    public XmlElement[] UnknownElements { get; set; }
}