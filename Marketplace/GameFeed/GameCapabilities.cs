using System.Xml;
using System.Xml.Serialization;

namespace XboxLib.Marketplace.GameFeed;

[XmlRoot(ElementName = "gameCapabilities", Namespace = Client.LiveNamespace)]
public class GameCapabilities
{
    [XmlElement(ElementName = "offlineCoopHardDriveRequired", Namespace = Client.LiveNamespace)]
    public bool? OfflineCoopHardDriveRequired { get; set; }

    [XmlElement(ElementName = "offlineMaxHDTVModeId", Namespace = Client.LiveNamespace)]
    public uint? OfflineMaxHdtvModeId { get; set; }

    [XmlElement(ElementName = "onlineMultiplayerMin", Namespace = Client.LiveNamespace)]
    public uint? OnlineMultiplayerMin { get; set; }

    [XmlElement(ElementName = "onlineMultiplayerMax", Namespace = Client.LiveNamespace)]
    public uint? OnlineMultiplayerMax { get; set; }

    [XmlElement(ElementName = "onlineMultiplayerHardDriveRequired", Namespace = Client.LiveNamespace)]
    public bool OnlineMultiplayerHardDriveRequired { get; set; }

    [XmlElement(ElementName = "onlineCoopPlayersMin", Namespace = Client.LiveNamespace)]
    public uint? OnlineCoopPlayersMin { get; set; }

    [XmlElement(ElementName = "onlineCoopPlayersMax", Namespace = Client.LiveNamespace)]
    public uint? OnlineCoopPlayersMax { get; set; }

    [XmlElement(ElementName = "onlineCoopHardDriveRequired", Namespace = Client.LiveNamespace)]
    public bool OnlineCoopHardDriveRequired { get; set; }

    [XmlElement(ElementName = "onlineHardDriveRequired", Namespace = Client.LiveNamespace)]
    public bool OnlineHardDriveRequired { get; set; }

    [XmlElement(ElementName = "onlineContentDownload", Namespace = Client.LiveNamespace)]
    public bool OnlineContentDownload { get; set; }

    [XmlElement(ElementName = "onlineLeaderboards", Namespace = Client.LiveNamespace)]
    public bool OnlineLeaderboards { get; set; }

    [XmlElement(ElementName = "offlinePlayersMin", Namespace = Client.LiveNamespace)]
    public uint? OfflinePlayersMin { get; set; }

    [XmlElement(ElementName = "offlinePlayersMax", Namespace = Client.LiveNamespace)]
    public uint? OfflinePlayersMax { get; set; }

    [XmlElement(ElementName = "offlineDolbyDigital", Namespace = Client.LiveNamespace)]
    public bool OfflineDolbyDigital { get; set; }

    [XmlElement(ElementName = "onlineVoice", Namespace = Client.LiveNamespace)]
    public bool OnlineVoice { get; set; }

    [XmlElement(ElementName = "offlineCoopPlayersMin", Namespace = Client.LiveNamespace)]
    public uint? OfflineCoopPlayersMin { get; set; }

    [XmlElement(ElementName = "offlineCoopPlayersMax", Namespace = Client.LiveNamespace)]
    public uint? OfflineCoopPlayersMax { get; set; }

    [XmlElement(ElementName = "offlineSystemLinkMin", Namespace = Client.LiveNamespace)]
    public uint? OfflineSystemLinkMin { get; set; }

    [XmlElement(ElementName = "offlineSystemLinkMax", Namespace = Client.LiveNamespace)]
    public uint? OfflineSystemLinkMax { get; set; }

    [XmlElement(ElementName = "offlinePeripheralCamera", Namespace = Client.LiveNamespace)]
    public bool OfflinePeripheralCamera { get; set; }

    [XmlAnyAttribute] public XmlAttribute[] UnknownAttributes { get; set; }
    [XmlAnyElement] public XmlElement[] UnknownElements { get; set; }
}