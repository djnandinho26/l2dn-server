﻿using System.Xml.Serialization;

namespace L2Dn.Model.DataPack;

[XmlRoot("list")]
public class XmlTeleportData: XmlBase
{
    [XmlElement("npc")]
    public List<XmlTeleporterNpc> Npcs { get; set; } = [];
}