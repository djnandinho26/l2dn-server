using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Conditions;
using L2Dn.GameServer.Utilities;

namespace L2Dn.GameServer.Handlers;

/**
 * @author Sdw
 */
public class ConditionHandler
{
	private readonly Map<String, Func<StatSet, ICondition>> _conditionHandlerFactories = new();

	private ConditionHandler()
	{
	}
	
	public void registerHandler(String name, Func<StatSet, ICondition> handlerFactory)
	{
		_conditionHandlerFactories.put(name, handlerFactory);
	}
	
	public Func<StatSet, ICondition> getHandlerFactory(String name)
	{
		return _conditionHandlerFactories.get(name);
	}
	
	public int size()
	{
		return _conditionHandlerFactories.size();
	}
	
	private static class SingletonHolder
	{
		public static readonly ConditionHandler INSTANCE = new ConditionHandler();
	}
	
	public static ConditionHandler getInstance()
	{
		return SingletonHolder.INSTANCE;
	}
}