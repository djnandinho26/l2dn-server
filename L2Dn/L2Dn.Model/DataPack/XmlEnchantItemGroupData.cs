﻿using System.Xml.Serialization;

namespace L2Dn.Model.DataPack;

[XmlRoot("list")]
public class XmlEnchantItemGroupData: XmlBase
{
    [XmlElement("enchantRateGroup")]
    public List<XmlEnchantRateGroup> EnchantRateGroups { get; set; } = [];

    [XmlElement("enchantScrollGroup")]
    public List<XmlEnchantScrollGroup> EnchantScrollGroups { get; set; } = [];
}