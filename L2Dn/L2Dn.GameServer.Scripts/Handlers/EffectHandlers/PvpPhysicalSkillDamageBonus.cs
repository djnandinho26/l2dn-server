using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Stats;
using L2Dn.Model.Enums;

namespace L2Dn.GameServer.Scripts.Handlers.EffectHandlers;

/**
 * @author Sdw
 */
public class PvpPhysicalSkillDamageBonus: AbstractStatPercentEffect
{
	public PvpPhysicalSkillDamageBonus(StatSet @params): base(@params, Stat.PVP_PHYSICAL_SKILL_DAMAGE)
	{
	}
}