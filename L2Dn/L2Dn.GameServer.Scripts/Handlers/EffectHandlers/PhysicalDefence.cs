using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Stats;
using L2Dn.Model.Enums;

namespace L2Dn.GameServer.Scripts.Handlers.EffectHandlers;

/**
 * @author Sdw
 */
public class PhysicalDefence: AbstractConditionalHpEffect
{
	public PhysicalDefence(StatSet @params): base(@params, Stat.PHYSICAL_DEFENCE)
	{
	}
}