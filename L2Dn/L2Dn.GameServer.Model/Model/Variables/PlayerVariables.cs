﻿using L2Dn.GameServer.Db;

namespace L2Dn.GameServer.Model.Variables;

public class PlayerVariables: AbstractVariables<CharacterVariable>
{
	// Public variable names.
	public const String INSTANCE_ORIGIN = "INSTANCE_ORIGIN";
	public const String INSTANCE_RESTORE = "INSTANCE_RESTORE";
	public const String RESTORE_LOCATION = "RESTORE_LOCATION";
	public const String HAIR_ACCESSORY_VARIABLE_NAME = "HAIR_ACCESSORY_ENABLED";
	public const String WORLD_CHAT_VARIABLE_NAME = "WORLD_CHAT_USED";
	public const String VITALITY_ITEMS_USED_VARIABLE_NAME = "VITALITY_ITEMS_USED";
	public const String UI_KEY_MAPPING = "UI_KEY_MAPPING";
	public const String CLIENT_SETTINGS = "CLIENT_SETTINGS";
	public const String ATTENDANCE_DATE = "ATTENDANCE_DATE";
	public const String ATTENDANCE_INDEX = "ATTENDANCE_INDEX";
	public const String ABILITY_POINTS_MAIN_CLASS = "ABILITY_POINTS";
	public const String ABILITY_POINTS_DUAL_CLASS = "ABILITY_POINTS_DUAL_CLASS";
	public const String ABILITY_POINTS_USED_MAIN_CLASS = "ABILITY_POINTS_USED";
	public const String ABILITY_POINTS_USED_DUAL_CLASS = "ABILITY_POINTS_DUAL_CLASS_USED";
	public const String REVELATION_SKILL_1_MAIN_CLASS = "RevelationSkill1";
	public const String REVELATION_SKILL_2_MAIN_CLASS = "RevelationSkill2";
	public const String REVELATION_SKILL_1_DUAL_CLASS = "DualclassRevelationSkill1";
	public const String REVELATION_SKILL_2_DUAL_CLASS = "DualclassRevelationSkill2";
	public const String LAST_PLEDGE_REPUTATION_LEVEL = "LAST_PLEDGE_REPUTATION_LEVEL";
	public const String FORTUNE_TELLING_VARIABLE = "FortuneTelling";
	public const String FORTUNE_TELLING_BLACK_CAT_VARIABLE = "FortuneTellingBlackCat";
	public const String DELUSION_RETURN = "DELUSION_RETURN";
	public const String AUTO_USE_SETTINGS = "AUTO_USE_SETTINGS";
	public const String AUTO_USE_SHORTCUTS = "AUTO_USE_SHORTCUTS";
	public const String LAST_HUNTING_ZONE_ID = "LAST_HUNTING_ZONE_ID";
	public const String HUNTING_ZONE_ENTRY = "HUNTING_ZONE_ENTRY_";
	public const String HUNTING_ZONE_TIME = "HUNTING_ZONE_TIME_";
	public const String HUNTING_ZONE_REMAIN_REFILL = "HUNTING_ZONE_REMAIN_REFILL_";
	public const String SAYHA_GRACE_SUPPORT_ENDTIME = "SAYHA_GRACE_SUPPORT_ENDTIME";
	public const String LIMITED_SAYHA_GRACE_ENDTIME = "LIMITED_SAYHA_GRACE_ENDTIME";
	public const String MAGIC_LAMP_EXP = "MAGIC_LAMP_EXP";
	public const String DEATH_POINT_COUNT = "DEATH_POINT_COUNT";
	public const String BEAST_POINT_COUNT = "BEAST_POINT_COUNT";
	public const String ASSASSINATION_POINT_COUNT = "ASSASSINATION_POINT_COUNT";
	public const String FAVORITE_TELEPORTS = "FAVORITE_TELEPORTS";
	public const String ELIXIRS_AVAILABLE = "ELIXIRS_AVAILABLE";
	public const String STAT_POINTS = "STAT_POINTS";
	public const String STAT_STR = "STAT_STR";
	public const String STAT_DEX = "STAT_DEX";
	public const String STAT_CON = "STAT_CON";
	public const String STAT_INT = "STAT_INT";
	public const String STAT_WIT = "STAT_WIT";
	public const String STAT_MEN = "STAT_MEN";
	public const String RESURRECT_BY_PAYMENT_COUNT = "RESURRECT_BY_PAYMENT_COUNT";
	public const String PURGE_LAST_CATEGORY = "PURGE_LAST_CATEGORY";
	public const String CLAN_JOIN_TIME = "CLAN_JOIN_TIME";
	public const String CLAN_DONATION_POINTS = "CLAN_DONATION_POINTS";
	public const String HENNA1_DURATION = "HENNA1_DURATION";
	public const String HENNA2_DURATION = "HENNA2_DURATION";
	public const String HENNA3_DURATION = "HENNA3_DURATION";
	public const String HENNA4_DURATION = "HENNA4_DURATION";
	public const String DYE_POTENTIAL_DAILY_STEP = "DYE_POTENTIAL_DAILY_STEP";
	public const String DYE_POTENTIAL_DAILY_COUNT = "DYE_POTENTIAL_DAILY_COUNT";
	public const String DYE_POTENTIAL_DAILY_COUNT_ENCHANT_RESET = "DYE_POTENTIAL_DAILY_COUNT_ENCHANT_RESET";
	public const String MISSION_LEVEL_PROGRESS = "MISSION_LEVEL_PROGRESS_";
	public const String BALOK_AVAILABLE_REWARD = "BALOK_AVAILABLE_REWARD";
	public const String DUAL_INVENTORY_SLOT = "DUAL_INVENTORY_SLOT";
	public const String DUAL_INVENTORY_SET_A = "DUAL_INVENTORY_SET_A";
	public const String DUAL_INVENTORY_SET_B = "DUAL_INVENTORY_SET_B";
	public const String DAILY_EXTRACT_ITEM = "DAILY_EXTRACT_ITEM";
	public const String SKILL_ENCHANT_STAR = "SKILL_ENCHANT_STAR_";
	public const String SKILL_TRY_ENCHANT = "SKILL_TRY_ENCHANT_";

	private readonly int _objectId;

	public PlayerVariables(int objectId)
	{
		_objectId = objectId;
		restoreMe();
	}

	protected override IQueryable<CharacterVariable> GetQuery(GameServerDbContext ctx)
	{
		return ctx.CharacterVariables.Where(r => r.CharacterId == _objectId);
	}

	protected override CharacterVariable CreateVar()
	{
		return new CharacterVariable()
		{
			CharacterId = _objectId
		};
	}
}