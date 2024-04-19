using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Items;
using L2Dn.GameServer.Model.Skills;
using L2Dn.Model.Enums;

namespace L2Dn.GameServer.Model.Conditions;

/**
 * The Class ConditionPlayerPledgeClass.
 * @author MrPoke
 */
public class ConditionPlayerPledgeClass : Condition
{
	private readonly SocialClass _pledgeClass;
	
	/**
	 * Instantiates a new condition player pledge class.
	 * @param pledgeClass the pledge class
	 */
	public ConditionPlayerPledgeClass(SocialClass pledgeClass)
	{
		_pledgeClass = pledgeClass;
	}
	
	/**
	 * Test impl.
	 * @return true, if successful
	 */
	public override bool testImpl(Creature effector, Creature effected, Skill skill, ItemTemplate item)
	{
		Player player = effector.getActingPlayer();
		if ((player == null) || (player.getClan() == null))
		{
			return false;
		}
		
		bool isClanLeader = player.isClanLeader();
		if ((_pledgeClass == (SocialClass)(-1)) && !isClanLeader)
		{
			return false;
		}
		
		return isClanLeader || (player.getPledgeClass() >= _pledgeClass);
	}
}
