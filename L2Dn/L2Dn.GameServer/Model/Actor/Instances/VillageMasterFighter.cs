﻿using L2Dn.GameServer.Data.Xml;
using L2Dn.GameServer.Enums;
using L2Dn.GameServer.Model.Actor.Templates;

namespace L2Dn.GameServer.Model.Actor.Instances;

public class VillageMasterFighter: VillageMaster
{
    /**
     * Creates a village master.
     * @param template the village master NPC template
     */
    public VillageMasterFighter(NpcTemplate template): base(template)
    {
    }

    protected sealed override bool checkVillageMasterRace(ClassId pClass)
    {
        return (pClass.getRace() == Race.HUMAN) || (pClass.getRace() == Race.ELF);
    }

    protected sealed override bool checkVillageMasterTeachType(ClassId pClass)
    {
        return CategoryData.getInstance().isInCategory(CategoryType.FIGHTER_GROUP, pClass);
    }
}