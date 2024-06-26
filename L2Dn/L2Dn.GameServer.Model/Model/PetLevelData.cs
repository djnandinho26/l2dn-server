﻿using L2Dn.GameServer.Model.Stats;
using L2Dn.Model.Enums;

namespace L2Dn.GameServer.Model;

/**
 * Stats definition for each pet level.
 * @author JIV, Zoey76
 */
public class PetLevelData
{
	private readonly int _ownerExpTaken;
	private readonly int _petFeedBattle;
	private readonly int _petFeedNormal;
	private readonly float _petMAtk;
	private readonly long _petMaxExp;
	private readonly int _petMaxFeed;
	private readonly float _petMaxHP;
	private readonly  float _petMaxMP;
	private readonly  float _petMDef;
	private readonly  float _petPAtk;
	private readonly  float _petPDef;
	private readonly  float _petRegenHP;
	private readonly  float _petRegenMP;
	private readonly  short _petSoulShot;
	private readonly  short _petSpiritShot;
	private readonly  double _walkSpeedOnRide;
	private readonly  double _runSpeedOnRide;
	private readonly  double _slowSwimSpeedOnRide;
	private readonly  double _fastSwimSpeedOnRide;
	private readonly  double _slowFlySpeedOnRide;
	private readonly  double _fastFlySpeedOnRide;
	
	public PetLevelData(StatSet set)
	{
		_ownerExpTaken = set.getInt("get_exp_type");
		_petMaxExp = (long) set.getDouble("exp");
		_petMaxHP = set.getFloat("org_hp");
		_petMaxMP = set.getFloat("org_mp");
		_petPAtk = set.getFloat("org_pattack");
		_petPDef = set.getFloat("org_pdefend");
		_petMAtk = set.getFloat("org_mattack");
		_petMDef = set.getFloat("org_mdefend");
		_petMaxFeed = set.getInt("max_meal");
		_petFeedBattle = set.getInt("consume_meal_in_battle");
		_petFeedNormal = set.getInt("consume_meal_in_normal");
		_petRegenHP = set.getFloat("org_hp_regen");
		_petRegenMP = set.getFloat("org_mp_regen");
		_petSoulShot = set.getShort("soulshot_count");
		_petSpiritShot = set.getShort("spiritshot_count");
		_walkSpeedOnRide = set.getDouble("walkSpeedOnRide", 0);
		_runSpeedOnRide = set.getDouble("runSpeedOnRide", 0);
		_slowSwimSpeedOnRide = set.getDouble("slowSwimSpeedOnRide", 0);
		_fastSwimSpeedOnRide = set.getDouble("fastSwimSpeedOnRide", 0);
		_slowFlySpeedOnRide = set.getDouble("slowFlySpeedOnRide", 0);
		_fastFlySpeedOnRide = set.getDouble("fastFlySpeedOnRide", 0);
	}
	
	/**
	 * @return the owner's experience points consumed by the pet.
	 */
	public int getOwnerExpTaken()
	{
		return _ownerExpTaken;
	}
	
	/**
	 * @return the pet's food consume rate at battle state.
	 */
	public int getPetFeedBattle()
	{
		return _petFeedBattle;
	}
	
	/**
	 * @return the pet's food consume rate at normal state.
	 */
	public int getPetFeedNormal()
	{
		return _petFeedNormal;
	}
	
	/**
	 * @return the pet's Magical Attack.
	 */
	public float getPetMAtk()
	{
		return _petMAtk;
	}
	
	/**
	 * @return the pet's maximum experience points.
	 */
	public long getPetMaxExp()
	{
		return _petMaxExp;
	}
	
	/**
	 * @return the pet's maximum feed points.
	 */
	public int getPetMaxFeed()
	{
		return _petMaxFeed;
	}
	
	/**
	 * @return the pet's maximum HP.
	 */
	public float getPetMaxHP()
	{
		return _petMaxHP;
	}
	
	/**
	 * @return the pet's maximum MP.
	 */
	public float getPetMaxMP()
	{
		return _petMaxMP;
	}
	
	/**
	 * @return the pet's Magical Defense.
	 */
	public float getPetMDef()
	{
		return _petMDef;
	}
	
	/**
	 * @return the pet's Physical Attack.
	 */
	public float getPetPAtk()
	{
		return _petPAtk;
	}
	
	/**
	 * @return the pet's Physical Defense.
	 */
	public float getPetPDef()
	{
		return _petPDef;
	}
	
	/**
	 * @return the pet's HP regeneration rate.
	 */
	public float getPetRegenHP()
	{
		return _petRegenHP;
	}
	
	/**
	 * @return the pet's MP regeneration rate.
	 */
	public float getPetRegenMP()
	{
		return _petRegenMP;
	}
	
	/**
	 * @return the pet's soulshot use count.
	 */
	public short getPetSoulShot()
	{
		return _petSoulShot;
	}
	
	/**
	 * @return the pet's spiritshot use count.
	 */
	public short getPetSpiritShot()
	{
		return _petSpiritShot;
	}
	
	/**
	 * @param stat movement type
	 * @return the base riding speed of given movement type.
	 */
	public double getSpeedOnRide(Stat stat)
	{
		switch (stat)
		{
			case Stat.WALK_SPEED:
			{
				return _walkSpeedOnRide;
			}
			case Stat.RUN_SPEED:
			{
				return _runSpeedOnRide;
			}
			case Stat.SWIM_WALK_SPEED:
			{
				return _slowSwimSpeedOnRide;
			}
			case Stat.SWIM_RUN_SPEED:
			{
				return _fastSwimSpeedOnRide;
			}
			case Stat.FLY_RUN_SPEED:
			{
				return _slowFlySpeedOnRide;
			}
			case Stat.FLY_WALK_SPEED:
			{
				return _fastFlySpeedOnRide;
			}
		}
		return 0;
	}
}