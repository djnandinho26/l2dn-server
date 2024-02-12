﻿using L2Dn.GameServer.Model.Interfaces;

namespace L2Dn.GameServer.Model.Variables;

public abstract class AbstractVariables: StatSet, IRestorable, IStorable, IDeletable
{
	private int _hasChanges;
	
	/**
	 * Overriding following methods to prevent from doing useless database operations if there is no changes since player's login.
	 */

	public override void set(String name, bool value)
	{
		_hasChanges = 1;
		base.set(name, value);
	}
	
	public override void set(String name, byte value)
	{
		_hasChanges = 1;
		base.set(name, value);
	}
	
	public override void set(String name, short value)
	{
		_hasChanges = 1;
		base.set(name, value);
	}
	
	public override void set(String name, int value)
	{
		_hasChanges = 1;
		base.set(name, value);
	}
	
	public override void set(String name, long value)
	{
		_hasChanges = 1;
		base.set(name, value);
	}
	
	public override void set(String name, float value)
	{
		_hasChanges = 1;
		base.set(name, value);
	}
	
	public override void set(String name, double value)
	{
		_hasChanges = 1;
		base.set(name, value);
	}
	
	public override void set(String name, String value)
	{
		_hasChanges = 1;
		base.set(name, value);
	}
	
	public override void set<T>(String name, T value)
	{
		_hasChanges = 1;
		base.set(name, value);
	}
	
	public override void set(String name, Object value)
	{
		_hasChanges = 1;
		base.set(name, value);
	}
	
	/**
	 * Put's entry to the variables and marks as changed if required (<i>Useful when restoring to do not save them again</i>).
	 * @param name
	 * @param value
	 * @param markAsChanged
	 */
	public void set(String name, String value, bool markAsChanged)
	{
		if (markAsChanged)
		{
			_hasChanges = 1;
		}
		
		base.set(name, value);
	}
	
	/**
	 * Return true if there exists a record for the variable name.
	 * @param name
	 * @return
	 */
	public bool hasVariable(String name)
	{
		return getSet().containsKey(name);
	}
	
	/**
	 * @return {@code true} if changes are made since last load/save.
	 */
	public bool hasChanges()
	{
		return _hasChanges != 0;
	}

	/**
	 * Atomically sets the value to the given updated value if the current value {@code ==} the expected value.
	 * @param expect
	 * @param update
	 * @return {@code true} if successful. {@code false} return indicates that the actual value was not equal to the expected value.
	 */
	public bool compareAndSetChanges(bool expect, bool update)
	{
		int expectInt = expect ? 1 : 0;
		return Interlocked.CompareExchange(ref _hasChanges, update ? 1 : 0, expectInt) == expectInt;
	}

	/**
	 * Removes variable
	 * @param name
	 */
	public override void remove(String name)
	{
		_hasChanges = 1;
		getSet().remove(name);
	}
}