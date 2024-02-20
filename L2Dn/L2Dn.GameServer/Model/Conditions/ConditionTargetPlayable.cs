using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Items;
using L2Dn.GameServer.Model.Skills;

namespace L2Dn.GameServer.Model.Conditions;

/**
 * @author DS
 */
public class ConditionTargetPlayable: Condition
{
	public override bool testImpl(Creature effector, Creature effected, Skill skill, ItemTemplate item)
	{
		return (effected != null) && effected.isPlayable();
	}
}
