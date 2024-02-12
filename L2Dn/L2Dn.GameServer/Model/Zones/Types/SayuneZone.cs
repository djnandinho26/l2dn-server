using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Actor.Tasks.PlayerTasks;

namespace L2Dn.GameServer.Model.Zones.Types;

/**
 * @author UnAfraid
 */
public class SayuneZone: ZoneType
{
	private int _mapId = -1;

	public SayuneZone(int id): base(id)
	{
	}

	public void setParameter(String name, String value)
	{
		switch (name)
		{
			case "mapId":
			{
				_mapId = int.Parse(value);
				break;
			}
			default:
			{
				base.setParameter(name, value);
			}
		}
	}

	protected override void onEnter(Creature creature)
	{
		if (creature.isPlayer() &&
		    ( /* creature.isInCategory(CategoryType.SIXTH_CLASS_GROUP) || */ Config.FREE_JUMPS_FOR_ALL) &&
		    !creature.getActingPlayer().isMounted() && !creature.isTransformed())
		{
			creature.setInsideZone(ZoneId.SAYUNE, true);
			ThreadPool.execute(new FlyMoveStartTask(this, creature.getActingPlayer()));
		}
	}

	protected override void onExit(Creature creature)
	{
		if (creature.isPlayer())
		{
			creature.setInsideZone(ZoneId.SAYUNE, false);
		}
	}

	public int getMapId()
	{
		return _mapId;
	}
}