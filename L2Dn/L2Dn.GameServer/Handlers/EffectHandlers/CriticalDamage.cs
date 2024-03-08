using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Stats;

namespace L2Dn.GameServer.Handlers.EffectHandlers;

/**
 * @author Sdw
 */
public class CriticalDamage: AbstractStatEffect
{
	public CriticalDamage(StatSet @params): base(@params, Stat.CRITICAL_DAMAGE, Stat.CRITICAL_DAMAGE_ADD)
	{
	}
}