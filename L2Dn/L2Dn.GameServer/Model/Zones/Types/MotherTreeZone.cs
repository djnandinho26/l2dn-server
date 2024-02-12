using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Utilities;

namespace L2Dn.GameServer.Model.Zones.Types;

/**
 * A mother-trees zone Basic type zone for Hp, MP regen
 * @author durgus
 */
public class MotherTreeZone : ZoneType
{
	private int _enterMsg;
	private int _leaveMsg;
	private int _mpRegen;
	private int _hpRegen;
	
	public MotherTreeZone(int id):base(id)
	{
	}
	
	public void setParameter(String name, String value)
	{
		if (name.equals("enterMsgId"))
		{
			_enterMsg = int.Parse(value);
		}
		else if (name.equals("leaveMsgId"))
		{
			_leaveMsg = int.Parse(value);
		}
		else if (name.equals("MpRegenBonus"))
		{
			_mpRegen = int.Parse(value);
		}
		else if (name.equals("HpRegenBonus"))
		{
			_hpRegen = int.Parse(value);
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
			Player player = creature.getActingPlayer();
			creature.setInsideZone(ZoneId.MOTHER_TREE, true);
			if (_enterMsg != 0)
			{
				player.sendPacket(new SystemMessage(_enterMsg));
			}
		}
	}
	
	protected override void onExit(Creature creature)
	{
		if (creature.isPlayer())
		{
			Player player = creature.getActingPlayer();
			player.setInsideZone(ZoneId.MOTHER_TREE, false);
			if (_leaveMsg != 0)
			{
				player.sendPacket(new SystemMessage(_leaveMsg));
			}
		}
	}
	
	/**
	 * @return the _mpRegen
	 */
	public int getMpRegenBonus()
	{
		return _mpRegen;
	}
	
	/**
	 * @return the _hpRegen
	 */
	public int getHpRegenBonus()
	{
		return _hpRegen;
	}
}