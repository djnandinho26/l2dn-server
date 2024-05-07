﻿using System.Xml.Serialization;

namespace L2Dn.Model.DataPack;

[XmlRoot("list")]
public class XmlEnchantChallengePointData: XmlBase
{
    [XmlElement("maxPoints")]
    public int MaxPoints { get; set; }

    [XmlElement("maxTicketCharge")]
    public int MaxTicketCharge { get; set; }

    [XmlArray("fees")]
    [XmlArrayItem("option")]
    public List<XmlEnchantChallengePointFeeOption> FeeOptions { get; set; } = [];

    [XmlArray("groups")]
    [XmlArrayItem("group")]
    public List<XmlEnchantChallengePointGroup> Groups { get; set; } = [];
}