using L2Dn.GameServer.AI;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Effects;
using L2Dn.GameServer.Model.Items.Instances;
using L2Dn.GameServer.Model.Skills;
using L2Dn.GameServer.Model.Stats;

namespace L2Dn.GameServer.Scripts.Handlers.EffectHandlers;

/**
 * @author Mobius
 */
public class Compelling: AbstractEffect
{
	public Compelling(StatSet @params)
	{
	}
	
	public override bool calcSuccess(Creature effector, Creature effected, Skill skill)
	{
		return Formulas.calcProbability(100, effector, effected, skill);
	}
	
	public override bool canStart(Creature effector, Creature effected, Skill skill)
	{
		if ((effected == null) || effected.isRaid() || (effected.isNpc() && !effected.isAttackable()))
		{
			return false;
		}
		
		return effected.isPlayable() || effected.isAttackable();
	}
	
	public override void onStart(Creature effector, Creature effected, Skill skill, Item item)
	{
		effected.getAI().notifyEvent(CtrlEvent.EVT_AFRAID);
		compellingAction(effector, effected);
	}
	
	public override bool onActionTime(Creature effector, Creature effected, Skill skill, Item item)
	{
		compellingAction(null, effected);
		return false;
	}
	
	public override void onExit(Creature effector, Creature effected, Skill skill)
	{
		if (!effected.isPlayer())
		{
			effected.getAI().notifyEvent(CtrlEvent.EVT_THINK);
		}
	}
	
	private void compellingAction(Creature effector, Creature effected)
	{
		effected.setRunning();
		effected.getAI().setIntention(CtrlIntention.AI_INTENTION_MOVE_TO, effector.Location.Location3D);
	}
}