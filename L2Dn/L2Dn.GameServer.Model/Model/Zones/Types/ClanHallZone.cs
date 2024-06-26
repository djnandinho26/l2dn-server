using L2Dn.GameServer.Data.Xml;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Residences;
using L2Dn.GameServer.Utilities;
using L2Dn.Geometry;

namespace L2Dn.GameServer.Model.Zones.Types;

/**
 * A clan hall zone
 * @author durgus
 */
public class ClanHallZone(int id): ResidenceZone(id)
{
	public override void setParameter(string name, string value)
	{
		if (name.equals("clanHallId"))
		{
			setResidenceId(int.Parse(value));
		}
		else
		{
			base.setParameter(name, value);
		}
	}

	protected override void onEnter(Creature creature)
	{
		if (creature.isPlayer())
		{
			creature.setInsideZone(ZoneId.CLAN_HALL, true);
		}
	}

	protected override void onExit(Creature creature)
	{
		if (creature.isPlayer())
		{
			creature.setInsideZone(ZoneId.CLAN_HALL, false);
		}
	}

	public override Location3D getBanishSpawnLoc()
	{
		ClanHall? clanHall = ClanHallData.getInstance().getClanHallById(getResidenceId());
		if (clanHall is null)
		{
			throw new InvalidOperationException("No clan hall in clan hall zone");
		}

		return clanHall.getBanishLocation();
	}
}