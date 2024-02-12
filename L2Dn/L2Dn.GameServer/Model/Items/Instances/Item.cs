using System.Runtime.CompilerServices;
using System.Text;
using L2Dn.GameServer.Data.Xml;
using L2Dn.GameServer.Enums;
using L2Dn.GameServer.InstanceManagers;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Actor.Request;
using L2Dn.GameServer.Model.Conditions;
using L2Dn.GameServer.Model.Ensoul;
using L2Dn.GameServer.Model.Events;
using L2Dn.GameServer.Model.Events.Impl.Creatures.Players;
using L2Dn.GameServer.Model.Events.Impl.Items;
using L2Dn.GameServer.Model.Geo;
using L2Dn.GameServer.Model.Holders;
using L2Dn.GameServer.Model.Items.Appearance;
using L2Dn.GameServer.Model.Items.Enchant.Attributes;
using L2Dn.GameServer.Model.Items.Types;
using L2Dn.GameServer.Model.Sieges;
using L2Dn.GameServer.Model.Variables;
using L2Dn.GameServer.Utilities;
using NLog;

namespace L2Dn.GameServer.Model.Items.Instances;

/**
 * This class manages items.
 * @version $Revision: 1.4.2.1.2.11 $ $Date: 2005/03/31 16:07:50 $
 */
public class Item: WorldObject
{
	private static readonly Logger LOGGER = LogManager.GetLogger(nameof(Item));
	private static readonly Logger LOG_ITEMS = LogManager.GetLogger("item");
	
	/** Owner */
	private int _ownerId;
	private Player _owner;
	
	/** ID of who dropped the item last, used for knownlist */
	private int _dropperObjectId = 0;
	
	/** Quantity of the item */
	private long _count = 1;
	/** Initial Quantity of the item */
	private long _initCount;
	/** Remaining time (in miliseconds) */
	private long _time;
	/** Quantity of the item can decrease */
	private bool _decrease = false;
	
	/** ID of the item */
	private readonly int _itemId;
	
	/** ItemTemplate associated to the item */
	private readonly ItemTemplate _itemTemplate;
	
	/** Location of the item : Inventory, PaperDoll, WareHouse */
	private ItemLocation _loc;
	
	/** Slot where item is stored : Paperdoll slot, inventory order ... */
	private int _locData;
	
	/** Level of enchantment of the item */
	private int _enchantLevel;
	
	/** Wear Item */
	private bool _wear;
	
	/** Augmented Item */
	private VariationInstance _augmentation = null;
	
	/** Shadow item */
	private int _mana = -1;
	private bool _consumingMana = false;
	
	/** Custom item types (used loto, race tickets) */
	private int _type1;
	private int _type2;
	
	private DateTime _dropTime;
	
	private bool _published = false;
	
	private bool _protected;
	
	public const int UNCHANGED = 0;
	public const int ADDED = 1;
	public const int REMOVED = 3;
	public const int MODIFIED = 2;
	
	//@formatter:off
	public static readonly int[] DEFAULT_ENCHANT_OPTIONS = { 0, 0, 0 };
	//@formatter:on
	
	private int _lastChange = 2; // 1 ??, 2 modified, 3 removed
	private bool _existsInDb; // if a record exists in DB.
	private bool _storedInDb; // if DB data is up-to-date.
	
	private readonly ReentrantLock _dbLock = new();
	
	private Map<AttributeType, AttributeHolder> _elementals = null;
	
	private ScheduledFuture _itemLootShedule = null;
	
	private readonly DropProtection _dropProtection = new DropProtection();
	
	private readonly List<Options.Options> _enchantOptions = new();
	private readonly EnsoulOption[] _ensoulOptions = new EnsoulOption[2];
	private readonly EnsoulOption[] _ensoulSpecialOptions = new EnsoulOption[1];
	private bool _isBlessed = false;
	
	/**
	 * Constructor of the Item from the objectId and the itemId.
	 * @param objectId : int designating the ID of the object in the world
	 * @param itemId : int designating the ID of the item
	 */
	public Item(int objectId, int itemId): base(objectId)
	{
		setInstanceType(InstanceType.Item);
		_itemId = itemId;
		_itemTemplate = ItemData.getInstance().getTemplate(itemId);
		if ((_itemId == 0) || (_itemTemplate == null))
		{
			throw new ArgumentException();
		}
		
		base.setName(_itemTemplate.getName());
		_loc = ItemLocation.VOID;
		_type1 = 0;
		_type2 = 0;
		_dropTime = DateTime.MinValue;
		_mana = _itemTemplate.getDuration();
		_time = _itemTemplate.getTime() == -1 ? -1 : System.currentTimeMillis() + (_itemTemplate.getTime() * 60 * 1000);
		scheduleLifeTimeTask();
		scheduleVisualLifeTime();
	}
	
	/**
	 * Constructor of the Item from the objetId and the description of the item given by the Item.
	 * @param objectId : int designating the ID of the object in the world
	 * @param itemTemplate : Item containing informations of the item
	 */
	public Item(int objectId, ItemTemplate itemTemplate): base(objectId)
	{
		setInstanceType(InstanceType.Item);
		_itemId = itemTemplate.getId();
		_itemTemplate = itemTemplate;
		if (_itemId == 0)
		{
			throw new ArgumentException();
		}
		
		base.setName(_itemTemplate.getName());
		_loc = ItemLocation.VOID;
		_mana = _itemTemplate.getDuration();
		_time = _itemTemplate.getTime() == -1 ? -1 : System.currentTimeMillis() + (_itemTemplate.getTime() * 60 * 1000);
		scheduleLifeTimeTask();
		scheduleVisualLifeTime();
	}
	
	/**
	 * @param rs
	 * @throws SQLException
	 */
	public Item(ResultSet rs): this(rs.getInt("object_id"), ItemData.getInstance().getTemplate(rs.getInt("item_id")))
	{
		_count = rs.getLong("count");
		_ownerId = rs.getInt("owner_id");
		_loc = ItemLocation.valueOf(rs.getString("loc"));
		_locData = rs.getInt("loc_data");
		_enchantLevel = rs.getInt("enchant_level");
		_type1 = rs.getInt("custom_type1");
		_type2 = rs.getInt("custom_type2");
		_mana = rs.getInt("mana_left");
		_time = rs.getLong("time");
		_existsInDb = true;
		_storedInDb = true;
		
		if (isEquipable())
		{
			restoreAttributes();
			restoreSpecialAbilities();
		}
		
		_isBlessed = getVariables().getBoolean(ItemVariables.BLESSED, false);
	}
	
	/**
	 * Constructor overload.<br>
	 * Sets the next free object ID in the ID factory.
	 * @param itemId the item template ID
	 */
	public Item(int itemId): this(IdManager.getInstance().getNextId(), itemId)
	{
	}
	
	/**
	 * Remove a Item from the world and send server->client GetItem packets.<br>
	 * <br>
	 * <b><u>Actions</u>:</b><br>
	 * <li>Send a Server->Client Packet GetItem to player that pick up and its _knowPlayers member</li>
	 * <li>Remove the WorldObject from the world</li><br>
	 * <font color=#FF0000><b><u>Caution</u>: This method DOESN'T REMOVE the object from _allObjects of World </b></font><br>
	 * <br>
	 * <b><u>Example of use</u>:</b><br>
	 * <li>Do Pickup Item : Player and Pet</li><br>
	 * @param creature Character that pick up the item
	 */
	public void pickupMe(Creature creature)
	{
		WorldRegion oldregion = getWorldRegion();
		
		// Create a server->client GetItem packet to pick up the Item
		creature.broadcastPacket(new GetItem(this, creature.getObjectId()));
		
		lock (this)
		{
			setSpawned(false);
		}
		
		// if this item is a mercenary ticket, remove the spawns!
		Castle castle = CastleManager.getInstance().getCastle(this);
		if ((castle != null) && (SiegeGuardManager.getInstance().getSiegeGuardByItem(castle.getResidenceId(), getId()) != null))
		{
			SiegeGuardManager.getInstance().removeTicket(this);
			ItemsOnGroundManager.getInstance().removeObject(this);
		}
		
		// outside of synchronized to avoid deadlocks
		// Remove the Item from the world
		World.getInstance().removeVisibleObject(this, oldregion);
		
		// Notify to scripts
		if (creature.isPlayer() && EventDispatcher.getInstance().hasListener(EventType.ON_PLAYER_ITEM_PICKUP, getTemplate()))
		{
			EventDispatcher.getInstance().notifyEventAsync(new OnPlayerItemPickup(creature.getActingPlayer(), this), getTemplate());
		}
	}
	
	/**
	 * Sets the ownerID of the item
	 * @param process : String Identifier of process triggering this action
	 * @param ownerId : int designating the ID of the owner
	 * @param creator : Player Player requesting the item creation
	 * @param reference : Object Object referencing current action like NPC selling item or previous item in transformation
	 */
	public void setOwnerId(String process, int ownerId, Player creator, Object reference)
	{
		setOwnerId(ownerId);
		
		if ((Config.LOG_ITEMS && ((!Config.LOG_ITEMS_SMALL_LOG) && (!Config.LOG_ITEMS_IDS_ONLY))) || (Config.LOG_ITEMS_SMALL_LOG && (_itemTemplate.isEquipable() || (_itemTemplate.getId() == ADENA_ID))) || (Config.LOG_ITEMS_IDS_ONLY && Config.LOG_ITEMS_IDS_LIST.contains(_itemTemplate.getId())))
		{
			if (_enchantLevel > 0)
			{
				StringBuilder sb = new StringBuilder();
				sb.Append("SETOWNER:");
				sb.Append(process);
				sb.Append(", item ");
				sb.Append(getObjectId());
				sb.Append(":+");
				sb.Append(_enchantLevel);
				sb.Append(" ");
				sb.Append(_itemTemplate.getName());
				sb.Append("(");
				sb.Append(_count);
				sb.Append("), ");
				sb.Append(creator);
				sb.Append(", ");
				sb.Append(reference);
				LOG_ITEMS.Info(sb.ToString());
			}
			else
			{
				StringBuilder sb = new StringBuilder();
				sb.Append("SETOWNER:");
				sb.Append(process);
				sb.Append(", item ");
				sb.Append(getObjectId());
				sb.Append(":");
				sb.Append(_itemTemplate.getName());
				sb.Append("(");
				sb.Append(_count);
				sb.Append("), ");
				sb.Append(creator);
				sb.Append(", ");
				sb.Append(reference);
				LOG_ITEMS.Info(sb.ToString());
			}
		}
		
		if ((creator != null) && creator.isGM() && Config.GMAUDIT)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(process);
			sb.Append("(id: ");
			sb.Append(_itemId);
			sb.Append(" name: ");
			sb.Append(getName());
			sb.Append(")");
			
			String targetName = (creator.getTarget() != null ? creator.getTarget().getName() : "no-target");
			
			String referenceName = "no-reference";
			if (reference is WorldObject)
			{
				referenceName = (((WorldObject) reference).getName() != null ? ((WorldObject) reference).getName() : "no-name");
			}
			else if (reference is String)
			{
				referenceName = (String) reference;
			}
			
			GMAudit.auditGMAction(creator.ToString(), sb.ToString(), targetName, StringUtil.concat("Object referencing this action is: ", referenceName));
		}
	}
	
	/**
	 * Sets the ownerID of the item
	 * @param ownerId : int designating the ID of the owner
	 */
	public void setOwnerId(int ownerId)
	{
		if (ownerId == _ownerId)
		{
			return;
		}
		
		// Remove any inventory skills from the old owner.
		removeSkillsFromOwner();
		
		_owner = null;
		_ownerId = ownerId;
		_storedInDb = false;
		
		// Give any inventory skills to the new owner only if the item is in inventory
		// else the skills will be given when location is set to inventory.
		giveSkillsToOwner();
	}
	
	/**
	 * Returns the ownerID of the item
	 * @return int : ownerID of the item
	 */
	public int getOwnerId()
	{
		return _ownerId;
	}
	
	/**
	 * Sets the location of the item
	 * @param loc : ItemLocation (enumeration)
	 */
	public void setItemLocation(ItemLocation loc)
	{
		setItemLocation(loc, 0);
	}
	
	/**
	 * Sets the location of the item.<br>
	 * <u><i>Remark :</i></u> If loc and loc_data different from database, say datas not up-to-date
	 * @param loc : ItemLocation (enumeration)
	 * @param locData : int designating the slot where the item is stored or the village for freights
	 */
	public void setItemLocation(ItemLocation loc, int locData)
	{
		if ((loc == _loc) && (locData == _locData))
		{
			return;
		}
		
		// Remove any inventory skills from the old owner.
		removeSkillsFromOwner();
		
		_loc = loc;
		_locData = locData;
		_storedInDb = false;
		
		// Give any inventory skills to the new owner only if the item is in inventory
		// else the skills will be given when location is set to inventory.
		giveSkillsToOwner();
	}
	
	public ItemLocation getItemLocation()
	{
		return _loc;
	}
	
	/**
	 * Sets the quantity of the item.
	 * @param count the new count to set
	 */
	public void setCount(long count)
	{
		if (_count == count)
		{
			return;
		}
		
		_count = count >= -1 ? count : 0;
		_storedInDb = false;
	}
	
	/**
	 * @return Returns the count.
	 */
	public long getCount()
	{
		return _count;
	}
	
	/**
	 * Sets the quantity of the item.<br>
	 * <u><i>Remark :</i></u> If loc and loc_data different from database, say datas not up-to-date
	 * @param process : String Identifier of process triggering this action
	 * @param count : int
	 * @param creator : Player Player requesting the item creation
	 * @param reference : Object Object referencing current action like NPC selling item or previous item in transformation
	 */
	public void changeCount(String process, long count, Player creator, Object reference)
	{
		if (count == 0)
		{
			return;
		}
		
		long old = _count;
		long max = _itemId == ADENA_ID ? MAX_ADENA : long.MaxValue;
		
		if ((count > 0) && (_count > (max - count)))
		{
			setCount(max);
		}
		else
		{
			setCount(_count + count);
		}
		
		if (_count < 0)
		{
			setCount(0);
		}
		
		_storedInDb = false;
		
		if ((Config.LOG_ITEMS && (process != null) && ((!Config.LOG_ITEMS_SMALL_LOG) && (!Config.LOG_ITEMS_IDS_ONLY))) || (Config.LOG_ITEMS_SMALL_LOG && (_itemTemplate.isEquipable() || (_itemTemplate.getId() == ADENA_ID))) || (Config.LOG_ITEMS_IDS_ONLY && Config.LOG_ITEMS_IDS_LIST.contains(_itemTemplate.getId())))
		{
			if (_enchantLevel > 0)
			{
				StringBuilder sb = new StringBuilder();
				sb.Append("CHANGE:");
				sb.Append(process);
				sb.Append(", item ");
				sb.Append(getObjectId());
				sb.Append(":+");
				sb.Append(_enchantLevel);
				sb.Append(" ");
				sb.Append(_itemTemplate.getName());
				sb.Append("(");
				sb.Append(_count);
				sb.Append("), PrevCount(");
				sb.Append(old);
				sb.Append("), ");
				sb.Append(creator);
				sb.Append(", ");
				sb.Append(reference);
				LOG_ITEMS.Info(sb.ToString());
			}
			else
			{
				StringBuilder sb = new StringBuilder();
				sb.Append("CHANGE:");
				sb.Append(process);
				sb.Append(", item ");
				sb.Append(getObjectId());
				sb.Append(":");
				sb.Append(_itemTemplate.getName());
				sb.Append("(");
				sb.Append(_count);
				sb.Append("), PrevCount(");
				sb.Append(old);
				sb.Append("), ");
				sb.Append(creator);
				sb.Append(", ");
				sb.Append(reference);
				LOG_ITEMS.Info(sb.ToString());
			}
		}
		
		if ((creator != null) && creator.isGM() && Config.GMAUDIT)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(process);
			sb.Append("(id: ");
			sb.Append(_itemId);
			sb.Append(" objId: ");
			sb.Append(getObjectId());
			sb.Append(" name: ");
			sb.Append(getName());
			sb.Append(" count: ");
			sb.Append(count);
			sb.Append(")");
			
			String targetName = (creator.getTarget() != null ? creator.getTarget().getName() : "no-target");
			
			String referenceName = "no-reference";
			if (reference is WorldObject)
			{
				referenceName = (((WorldObject) reference).getName() != null ? ((WorldObject) reference).getName() : "no-name");
			}
			else if (reference is String)
			{
				referenceName = (String) reference;
			}
			
			GMAudit.auditGMAction(creator.ToString(), sb.ToString(), targetName, StringUtil.concat("Object referencing this action is: ", referenceName));
		}
	}
	
	// No logging (function designed for shots only)
	public void changeCountWithoutTrace(int count, Player creator, Object reference)
	{
		changeCount(null, count, creator, reference);
	}
	
	/**
	 * Return true if item can be enchanted
	 * @return bool
	 */
	public bool isEnchantable()
	{
		if ((_loc == ItemLocation.INVENTORY) || (_loc == ItemLocation.PAPERDOLL))
		{
			return _itemTemplate.isEnchantable();
		}
		return false;
	}
	
	/**
	 * Returns if item is equipable
	 * @return bool
	 */
	public bool isEquipable()
	{
		return _itemTemplate.getBodyPart() != ItemTemplate.SLOT_NONE;
	}
	
	/**
	 * Returns if item is equipped
	 * @return bool
	 */
	public bool isEquipped()
	{
		return (_loc == ItemLocation.PAPERDOLL) || (_loc == ItemLocation.PET_EQUIP);
	}
	
	/**
	 * Returns the slot where the item is stored
	 * @return int
	 */
	public int getLocationSlot()
	{
		return _locData;
	}
	
	/**
	 * Returns the characteristics of the item.
	 * @return ItemTemplate
	 */
	public ItemTemplate getTemplate()
	{
		return _itemTemplate;
	}
	
	public int getCustomType1()
	{
		return _type1;
	}
	
	public int getCustomType2()
	{
		return _type2;
	}
	
	public void setCustomType1(int newtype)
	{
		_type1 = newtype;
	}
	
	public void setCustomType2(int newtype)
	{
		_type2 = newtype;
	}
	
	public void setDropTime(DateTime time)
	{
		_dropTime = time;
	}
	
	public DateTime getDropTime()
	{
		return _dropTime;
	}
	
	/**
	 * @return the type of item.
	 */
	public ItemType getItemType()
	{
		return _itemTemplate.getItemType();
	}
	
	/**
	 * Gets the item ID.
	 * @return the item ID
	 */
	public override int getId()
	{
		return _itemId;
	}
	
	/**
	 * @return the display Id of the item.
	 */
	public int getDisplayId()
	{
		return _itemTemplate.getDisplayId();
	}
	
	/**
	 * @return {@code true} if item is an EtcItem, {@code false} otherwise.
	 */
	public bool isEtcItem()
	{
		return (_itemTemplate is EtcItem);
	}
	
	/**
	 * @return {@code true} if item is a Weapon/Shield, {@code false} otherwise.
	 */
	public bool isWeapon()
	{
		return (_itemTemplate is Weapon);
	}
	
	/**
	 * @return {@code true} if item is an Armor, {@code false} otherwise.
	 */
	public bool isArmor()
	{
		return (_itemTemplate is Armor);
	}
	
	/**
	 * @return the characteristics of the EtcItem, {@code false} otherwise.
	 */
	public EtcItem getEtcItem()
	{
		if (_itemTemplate is EtcItem)
		{
			return (EtcItem) _itemTemplate;
		}
		return null;
	}
	
	/**
	 * @return the characteristics of the Weapon.
	 */
	public Weapon getWeaponItem()
	{
		if (_itemTemplate is Weapon)
		{
			return (Weapon) _itemTemplate;
		}
		return null;
	}
	
	/**
	 * @return the characteristics of the Armor.
	 */
	public Armor getArmorItem()
	{
		if (_itemTemplate is Armor)
		{
			return (Armor) _itemTemplate;
		}
		return null;
	}
	
	/**
	 * @return the quantity of crystals for crystallization.
	 */
	public int getCrystalCount()
	{
		return _itemTemplate.getCrystalCount(_enchantLevel);
	}
	
	/**
	 * @return the reference price of the item.
	 */
	public long getReferencePrice()
	{
		return _itemTemplate.getReferencePrice();
	}
	
	/**
	 * @return the name of the item.
	 */
	public String getItemName()
	{
		return _itemTemplate.getName();
	}
	
	/**
	 * @return the reuse delay of this item.
	 */
	public int getReuseDelay()
	{
		return _itemTemplate.getReuseDelay();
	}
	
	/**
	 * @return the shared reuse item group.
	 */
	public int getSharedReuseGroup()
	{
		return _itemTemplate.getSharedReuseGroup();
	}
	
	/**
	 * @return the last change of the item
	 */
	public int getLastChange()
	{
		return _lastChange;
	}
	
	/**
	 * Sets the last change of the item
	 * @param lastChange : int
	 */
	public void setLastChange(int lastChange)
	{
		_lastChange = lastChange;
	}
	
	/**
	 * Returns if item is stackable
	 * @return bool
	 */
	public bool isStackable()
	{
		return _itemTemplate.isStackable();
	}
	
	/**
	 * Returns if item is dropable
	 * @return bool
	 */
	public bool isDropable()
	{
		if (Config.ALT_ALLOW_AUGMENT_TRADE && isAugmented())
		{
			return true;
		}
		return !isAugmented() && (getVisualId() == 0) && _itemTemplate.isDropable();
	}
	
	/**
	 * Returns if item is destroyable
	 * @return bool
	 */
	public bool isDestroyable()
	{
		if (!Config.ALT_ALLOW_AUGMENT_DESTROY && isAugmented())
		{
			return false;
		}
		return _itemTemplate.isDestroyable();
	}
	
	/**
	 * Returns if item is tradeable
	 * @return bool
	 */
	public bool isTradeable()
	{
		if (Config.ALT_ALLOW_AUGMENT_TRADE && isAugmented())
		{
			return true;
		}
		return !isAugmented() && _itemTemplate.isTradeable();
	}
	
	/**
	 * Returns if item is sellable
	 * @return bool
	 */
	public bool isSellable()
	{
		if (Config.ALT_ALLOW_AUGMENT_TRADE && isAugmented())
		{
			return true;
		}
		return !isAugmented() && _itemTemplate.isSellable();
	}
	
	/**
	 * @param isPrivateWareHouse
	 * @return if item can be deposited in warehouse or freight
	 */
	public bool isDepositable(bool isPrivateWareHouse)
	{
		// equipped, hero and quest items
		if (isEquipped() || !_itemTemplate.isDepositable())
		{
			return false;
		}
		// augmented not tradeable
		if (!isPrivateWareHouse && (!isTradeable() || isShadowItem()))
		{
			return false;
		}
		return true;
	}
	
	public bool isPotion()
	{
		return _itemTemplate.isPotion();
	}
	
	public bool isElixir()
	{
		return _itemTemplate.isElixir();
	}
	
	public bool isScroll()
	{
		return _itemTemplate.isScroll();
	}
	
	public bool isHeroItem()
	{
		return _itemTemplate.isHeroItem();
	}
	
	public bool isCommonItem()
	{
		return _itemTemplate.isCommon();
	}
	
	/**
	 * Returns whether this item is pvp or not
	 * @return bool
	 */
	public bool isPvp()
	{
		return _itemTemplate.isPvpItem();
	}
	
	public bool isOlyRestrictedItem()
	{
		return _itemTemplate.isOlyRestrictedItem();
	}

	/**
	 * @param player
	 * @param allowAdena
	 * @param allowNonTradeable
	 * @return if item is available for manipulation
	 */
	public bool isAvailable(Player player, bool allowAdena, bool allowNonTradeable)
	{
		Summon pet = player.getPet();

		return ((!isEquipped()) // Not equipped
		        && (_itemTemplate.getType2() != ItemTemplate.TYPE2_QUEST) // Not Quest Item
		        && ((_itemTemplate.getType2() != ItemTemplate.TYPE2_MONEY) ||
		            (_itemTemplate.getType1() != ItemTemplate.TYPE1_SHIELD_ARMOR)) // not money, not shield
		        && ((pet == null) ||
		            (getObjectId() != pet.getControlObjectId())) // Not Control item of currently summoned pet
		        && !(player.isProcessingItem(getObjectId())) // Not momentarily used enchant scroll
		        && (allowAdena || (_itemId != ADENA_ID)) // Not Adena
		        && (!player.isCastingNow(s => s.getSkill().getItemConsumeId() != _itemId)) && (allowNonTradeable ||
			        (isTradeable() && (!((_itemTemplate.getItemType() == EtcItemType.PET_COLLAR) &&
			                             player.havePetInvItems())))));
	}

	/**
	 * Returns the level of enchantment of the item
	 * @return int
	 */
	public int getEnchantLevel()
	{
		return _enchantLevel;
	}
	
	/**
	 * @return {@code true} if item is enchanted, {@code false} otherwise
	 */
	public bool isEnchanted()
	{
		return _enchantLevel > 0;
	}
	
	/**
	 * @param level the enchant value to set
	 */
	public void setEnchantLevel(int level)
	{
		int newLevel = Math.Max(0, level);
		if (_enchantLevel == newLevel)
		{
			return;
		}
		
		clearEnchantStats();
		
		// Agathion skills.
		if (isEquipped() && (_itemTemplate.getBodyPart() == ItemTemplate.SLOT_AGATHION))
		{
			AgathionSkillHolder agathionSkills = AgathionData.getInstance().getSkills(getId());
			if (agathionSkills != null)
			{
				bool update = false;
				// Remove old skills.
				foreach (Skill skill in agathionSkills.getMainSkills(_enchantLevel))
				{
					getActingPlayer().removeSkill(skill, false, skill.isPassive());
					update = true;
				}
				foreach (Skill skill in agathionSkills.getSubSkills(_enchantLevel))
				{
					getActingPlayer().removeSkill(skill, false, skill.isPassive());
					update = true;
				}
				// Add new skills.
				if (getLocationSlot() == Inventory.PAPERDOLL_AGATHION1)
				{
					foreach (Skill skill in agathionSkills.getMainSkills(newLevel))
					{
						if (skill.isPassive() && !skill.checkConditions(SkillConditionScope.PASSIVE, getActingPlayer(), getActingPlayer()))
						{
							continue;
						}
						getActingPlayer().addSkill(skill, false);
						update = true;
					}
				}
				foreach (Skill skill in agathionSkills.getSubSkills(newLevel))
				{
					if (skill.isPassive() && !skill.checkConditions(SkillConditionScope.PASSIVE, getActingPlayer(), getActingPlayer()))
					{
						continue;
					}
					getActingPlayer().addSkill(skill, false);
					update = true;
				}
				if (update)
				{
					getActingPlayer().sendSkillList();
				}
			}
		}
		
		_enchantLevel = newLevel;
		applyEnchantStats();
		_storedInDb = false;
		
		getActingPlayer().getInventory().getPaperdollCache().clearMaxSetEnchant();
	}
	
	/**
	 * Returns whether this item is augmented or not
	 * @return true if augmented
	 */
	public bool isAugmented()
	{
		return _augmentation != null;
	}
	
	/**
	 * Returns the augmentation object for this item
	 * @return augmentation
	 */
	public VariationInstance getAugmentation()
	{
		return _augmentation;
	}
	
	/**
	 * Sets a new augmentation
	 * @param augmentation
	 * @param updateDatabase
	 * @return return true if successfully
	 */
	public bool setAugmentation(VariationInstance augmentation, bool updateDatabase)
	{
		// Remove previous augmentation.
		if (_augmentation != null)
		{
			if (isEquipped())
			{
				_augmentation.removeBonus(getActingPlayer());
			}
			removeAugmentation();
		}
		
		_augmentation = augmentation;
		if (updateDatabase)
		{
			updateItemOptions();
		}
		
		// Notify to scripts.
		if (EventDispatcher.getInstance().hasListener(EventType.ON_PLAYER_AUGMENT, getTemplate()))
		{
			EventDispatcher.getInstance().notifyEventAsync(new OnPlayerAugment(getActingPlayer(), this, augmentation, true), getTemplate());
		}
		
		return true;
	}
	
	/**
	 * Remove the augmentation
	 */
	public void removeAugmentation()
	{
		if (_augmentation == null)
		{
			return;
		}
		
		// Copy augmentation before removing it.
		VariationInstance augment = _augmentation;
		_augmentation = null;
		
		try 
		{
			using GameServerDbContext ctx = new();
			PreparedStatement ps = con.prepareStatement("DELETE FROM item_variations WHERE itemId = ?");
			ps.setInt(1, getObjectId());
			ps.executeUpdate();
		}
		catch (Exception e)
		{
			LOGGER.Error("Item could not remove augmentation for " + this + " from DB: " + e);
		}
		
		// Notify to scripts.
		if (EventDispatcher.getInstance().hasListener(EventType.ON_PLAYER_AUGMENT, getTemplate()))
		{
			EventDispatcher.getInstance().notifyEventAsync(new OnPlayerAugment(getActingPlayer(), this, augment, false), getTemplate());
		}
	}
	
	public void restoreAttributes()
	{
		try 
		{
			using GameServerDbContext ctx = new();
			PreparedStatement ps1 = con.prepareStatement("SELECT mineralId,option1,option2 FROM item_variations WHERE itemId=?");
			PreparedStatement ps2 =
				con.prepareStatement("SELECT elemType,elemValue FROM item_elementals WHERE itemId=?");
			ps1.setInt(1, getObjectId());
			{
				ResultSet rs = ps1.executeQuery();
				if (rs.next())
				{
					int mineralId = rs.getInt("mineralId");
					int option1 = rs.getInt("option1");
					int option2 = rs.getInt("option2");
					if ((option1 != -1) || (option2 != -1))
					{
						_augmentation = new VariationInstance(mineralId, option1, option2);
					}
				}
			}
			
			ps2.setInt(1, getObjectId());
			{
				ResultSet rs = ps2.executeQuery();
				while (rs.next())
				{
					byte attributeType = rs.getByte(1);
					int attributeValue = rs.getInt(2);
					if ((attributeType != -1) && (attributeValue != -1))
					{
						applyAttribute(new AttributeHolder(AttributeType.findByClientId(attributeType), attributeValue));
					}
				}
			}
		}
		catch (Exception e)
		{
			LOGGER.Error("Item could not restore augmentation and elemental data for " + this + " from DB: " + e);
		}
	}
	
	public void updateItemOptions()
	{
		try
		{
			using GameServerDbContext ctx = new();
			updateItemOptions(con);
		}
		catch (Exception e)
		{
			LOGGER.Warn("Item could not update atributes for " + this + " from DB:" + e);
		}
	}
	
	private void updateItemOptions(Connection con)
	{
		try
		{
			PreparedStatement ps = con.prepareStatement("REPLACE INTO item_variations VALUES(?,?,?,?)");
			ps.setInt(1, getObjectId());
			ps.setInt(2, _augmentation != null ? _augmentation.getMineralId() : 0);
			ps.setInt(3, _augmentation != null ? _augmentation.getOption1Id() : -1);
			ps.setInt(4, _augmentation != null ? _augmentation.getOption2Id() : -1);
			ps.executeUpdate();
		}
		catch (Exception e)
		{
			LOGGER.Error("Item could not update atributes for " + this + " from DB: " + e);
		}
	}
	
	public void updateItemElementals()
	{
		try
		{
			using GameServerDbContext ctx = new();
			updateItemElements(con);
		}
		catch (Exception e)
		{
			LOGGER.Error("Item could not update elementals for " + this + " from DB: " + e);
		}
	}
	
	private void updateItemElements(Connection con)
	{
		try
		{
			PreparedStatement ps = con.prepareStatement("DELETE FROM item_elementals WHERE itemId = ?");
			ps.setInt(1, getObjectId());
			ps.executeUpdate();
		}
		catch (Exception e)
		{
			LOGGER.Error("Item could not update elementals for " + this + " from DB: " + e);
		}
		
		if (_elementals == null)
		{
			return;
		}
		
		try
		{
			PreparedStatement ps = con.prepareStatement("INSERT INTO item_elementals VALUES(?,?,?)");
			foreach (AttributeHolder attribute in _elementals.values())
			{
				ps.setInt(1, getObjectId());
				ps.setByte(2, attribute.getType().getClientId());
				ps.setInt(3, attribute.getValue());
				ps.executeUpdate();
				ps.clearParameters();
			}
		}
		catch (Exception e)
		{
			LOGGER.Error("Item could not update elementals for " + this + " from DB: " + e);
		}
	}
	
	public ICollection<AttributeHolder> getAttributes()
	{
		return _elementals != null ? _elementals.values() : null;
	}
	
	public bool hasAttributes()
	{
		return (_elementals != null) && !_elementals.isEmpty();
	}
	
	public AttributeHolder getAttribute(AttributeType type)
	{
		return _elementals != null ? _elementals.get(type) : null;
	}
	
	public AttributeHolder getAttackAttribute()
	{
		if (isWeapon())
		{
			if (_itemTemplate.getAttributes() != null)
			{
				if (!_itemTemplate.getAttributes().isEmpty())
				{
					return _itemTemplate.getAttributes().iterator().next();
				}
			}
			else if ((_elementals != null) && !_elementals.isEmpty())
			{
				return _elementals.values().iterator().next();
			}
		}
		return null;
	}
	
	public AttributeType getAttackAttributeType()
	{
		AttributeHolder holder = getAttackAttribute();
		return holder != null ? holder.getType() : AttributeType.NONE;
	}
	
	public int getAttackAttributePower()
	{
		AttributeHolder holder = getAttackAttribute();
		return holder != null ? holder.getValue() : 0;
	}
	
	public int getDefenceAttribute(AttributeType element)
	{
		if (isArmor())
		{
			if (_itemTemplate.getAttributes() != null)
			{
				AttributeHolder attribute = _itemTemplate.getAttribute(element);
				if (attribute != null)
				{
					return attribute.getValue();
				}
			}
			else if (_elementals != null)
			{
				AttributeHolder attribute = getAttribute(element);
				if (attribute != null)
				{
					return attribute.getValue();
				}
			}
		}
		return 0;
	}
	
	[MethodImpl(MethodImplOptions.Synchronized)]
	private void applyAttribute(AttributeHolder holder)
	{
		if (_elementals == null)
		{
			_elementals = new();
			_elementals.put(holder.getType(), holder);
		}
		else
		{
			AttributeHolder attribute = getAttribute(holder.getType());
			if (attribute != null)
			{
				attribute.setValue(holder.getValue());
			}
			else
			{
				_elementals.put(holder.getType(), holder);
			}
		}
	}
	
	/**
	 * Add elemental attribute to item and save to db
	 * @param holder
	 * @param updateDatabase
	 */
	public void setAttribute(AttributeHolder holder, bool updateDatabase)
	{
		applyAttribute(holder);
		if (updateDatabase)
		{
			updateItemElementals();
		}
	}
	
	/**
	 * Remove elemental from item
	 * @param type byte element to remove
	 */
	public void clearAttribute(AttributeType type)
	{
		if ((_elementals == null) || (getAttribute(type) == null))
		{
			return;
		}
		
		lock (_elementals)
		{
			_elementals.remove(type);
		}
		
		try 
		{
			using GameServerDbContext ctx = new();
			PreparedStatement ps =
				con.prepareStatement("DELETE FROM item_elementals WHERE itemId = ? AND elemType = ?");
			ps.setInt(1, getObjectId());
			ps.setByte(2, type.getClientId());
			ps.executeUpdate();
		}
		catch (Exception e)
		{
			LOGGER.Error("Item could not remove elemental enchant for " + this + " from DB: " + e);
		}
	}
	
	public void clearAllAttributes()
	{
		if (_elementals == null)
		{
			return;
		}
		
		lock (_elementals)
		{
			_elementals.clear();
		}
		
		try 
		{
			using GameServerDbContext ctx = new();
			PreparedStatement ps = con.prepareStatement("DELETE FROM item_elementals WHERE itemId = ?");
			ps.setInt(1, getObjectId());
			ps.executeUpdate();
		}
		catch (Exception e)
		{
			LOGGER.Error("Item could not remove all elemental enchant for " + this + " from DB: " + e);
		}
	}
	
	/**
	 * Returns true if this item is a shadow item Shadow items have a limited life-time
	 * @return
	 */
	public bool isShadowItem()
	{
		return (_mana >= 0);
	}
	
	/**
	 * Returns the remaining mana of this shadow item
	 * @return lifeTime
	 */
	public int getMana()
	{
		return _mana;
	}
	
	/**
	 * Decreases the mana of this shadow item, sends a inventory update schedules a new consumption task if non is running optionally one could force a new task
	 * @param resetConsumingMana if true forces a new consumption task if item is equipped
	 */
	public void decreaseMana(bool resetConsumingMana)
	{
		decreaseMana(resetConsumingMana, 1);
	}
	
	/**
	 * Decreases the mana of this shadow item, sends a inventory update schedules a new consumption task if non is running optionally one could force a new task
	 * @param resetConsumingMana if forces a new consumption task if item is equipped
	 * @param count how much mana decrease
	 */
	public void decreaseMana(bool resetConsumingMana, int count)
	{
		if (!isShadowItem())
		{
			return;
		}
		
		if ((_mana - count) >= 0)
		{
			_mana -= count;
		}
		else
		{
			_mana = 0;
		}
		
		if (_storedInDb)
		{
			_storedInDb = false;
		}
		if (resetConsumingMana)
		{
			_consumingMana = false;
		}
		
		Player player = getActingPlayer();
		if (player != null)
		{
			SystemMessage sm;
			switch (_mana)
			{
				case 10:
				{
					sm = new SystemMessage(SystemMessageId.S1_S_REMAINING_MANA_IS_NOW_10);
					sm.addItemName(_itemTemplate);
					player.sendPacket(sm);
					break;
				}
				case 5:
				{
					sm = new SystemMessage(SystemMessageId.S1_S_REMAINING_MANA_IS_NOW_5);
					sm.addItemName(_itemTemplate);
					player.sendPacket(sm);
					break;
				}
				case 1:
				{
					sm = new SystemMessage(SystemMessageId.S1_S_REMAINING_MANA_IS_NOW_1_IT_WILL_DISAPPEAR_SOON);
					sm.addItemName(_itemTemplate);
					player.sendPacket(sm);
					break;
				}
			}
			
			if (_mana == 0) // The life time has expired
			{
				sm = new SystemMessage(SystemMessageId.S1_S_REMAINING_MANA_IS_NOW_0_AND_THE_ITEM_HAS_DISAPPEARED);
				sm.addItemName(_itemTemplate);
				player.sendPacket(sm);
				
				// unequip
				if (isEquipped())
				{
					InventoryUpdate iu = new InventoryUpdate();
					foreach (Item item in player.getInventory().unEquipItemInSlotAndRecord(getLocationSlot()))
					{
						iu.addModifiedItem(item);
					}
					player.sendInventoryUpdate(iu);
					player.broadcastUserInfo();
				}
				
				if (_loc != ItemLocation.WAREHOUSE)
				{
					// destroy
					player.getInventory().destroyItem("Item", this, player, null);
					
					// send update
					InventoryUpdate iu = new InventoryUpdate();
					iu.addRemovedItem(this);
					player.sendInventoryUpdate(iu);
				}
				else
				{
					player.getWarehouse().destroyItem("Item", this, player, null);
				}
			}
			else
			{
				// Reschedule if still equipped
				if (!_consumingMana && isEquipped())
				{
					scheduleConsumeManaTask();
				}
				if (_loc != ItemLocation.WAREHOUSE)
				{
					InventoryUpdate iu = new InventoryUpdate();
					iu.addModifiedItem(this);
					player.sendInventoryUpdate(iu);
				}
			}
		}
	}
	
	public void scheduleConsumeManaTask()
	{
		if (_consumingMana)
		{
			return;
		}
		_consumingMana = true;
		ItemManaTaskManager.getInstance().add(this);
	}
	
	/**
	 * Returns false cause item can't be attacked
	 * @return bool false
	 */
	public override bool isAutoAttackable(Creature attacker)
	{
		return false;
	}
	
	/**
	 * Updates the database.
	 */
	public void updateDatabase()
	{
		updateDatabase(false);
	}
	
	/**
	 * Updates the database.
	 * @param force if the update should necessarilly be done.
	 */
	public void updateDatabase(bool force)
	{
		_dbLock.lock();
		
		try
		{
			if (_existsInDb)
			{
				if ((_ownerId == 0) || (_loc == ItemLocation.VOID) || (_loc == ItemLocation.REFUND) || ((_count == 0) && (_loc != ItemLocation.LEASE)))
				{
					removeFromDb();
				}
				else if (!Config.LAZY_ITEMS_UPDATE || force)
				{
					updateInDb();
				}
			}
			else
			{
				if ((_ownerId == 0) || (_loc == ItemLocation.VOID) || (_loc == ItemLocation.REFUND) || ((_count == 0) && (_loc != ItemLocation.LEASE)))
				{
					return;
				}
				insertIntoDb();
			}
		}
		finally
		{
			_dbLock.unlock();
		}
	}
	
	/**
	 * Init a dropped Item and add it in the world as a visible object.<br>
	 * <br>
	 * <b><u>Actions</u>:</b><br>
	 * <li>Set the x,y,z position of the Item dropped and update its _worldregion</li>
	 * <li>Add the Item dropped to _visibleObjects of its WorldRegion</li>
	 * <li>Add the Item dropped in the world as a <b>visible</b> object</li><br>
	 * <font color=#FF0000><b><u>Caution</u>: This method DOESN'T ADD the object to _allObjects of World </b></font><br>
	 * <br>
	 * <b><u>Example of use</u>:</b><br>
	 * <li>Drop item</li>
	 * <li>Call Pet</li>
	 * @param dropper
	 * @param locX
	 * @param locY
	 * @param locZ
	 */
	public void dropMe(Creature dropper, int locX, int locY, int locZ)
	{
		int x = locX;
		int y = locY;
		int z = locZ;
		
		if (dropper != null)
		{
			Instance instance = dropper.getInstanceWorld();
			Location dropDest = GeoEngine.getInstance().getValidLocation(dropper.getX(), dropper.getY(), dropper.getZ(), x, y, z, instance);
			x = dropDest.getX();
			y = dropDest.getY();
			z = dropDest.getZ();
			setInstance(instance); // Inherit instancezone when dropped in visible world
		}
		else
		{
			setInstance(null); // No dropper? Make it a global item...
		}
		
		// Set the x,y,z position of the Item dropped and update its world region
		setSpawned(true);
		setXYZ(x, y, z);
		
		setDropTime(System.currentTimeMillis());
		setDropperObjectId(dropper != null ? dropper.getObjectId() : 0); // Set the dropper Id for the knownlist packets in sendInfo
		
		// Add the Item dropped in the world as a visible object
		WorldRegion region = getWorldRegion();
		region.addVisibleObject(this);
		World.getInstance().addVisibleObject(this, region);
		if (Config.SAVE_DROPPED_ITEM)
		{
			ItemsOnGroundManager.getInstance().save(this);
		}
		setDropperObjectId(0); // Set the dropper Id back to 0 so it no longer shows the drop packet
		
		if ((dropper != null) && dropper.isPlayer())
		{
			_owner = null;
			
			// Notify to scripts
			if (EventDispatcher.getInstance().hasListener(EventType.ON_PLAYER_ITEM_DROP, getTemplate()))
			{
				EventDispatcher.getInstance().notifyEventAsync(new OnPlayerItemDrop(dropper.getActingPlayer(), this, new Location(x, y, z)), getTemplate());
			}
		}
	}
	
	/**
	 * Update the database with values of the item
	 */
	private void updateInDb()
	{
		if (!_existsInDb || _wear || _storedInDb)
		{
			return;
		}
		
		try 
		{
			using GameServerDbContext ctx = new();
			PreparedStatement ps = con.prepareStatement(
				"UPDATE items SET owner_id=?,count=?,loc=?,loc_data=?,enchant_level=?,custom_type1=?,custom_type2=?,mana_left=?,time=? WHERE object_id = ?");
			ps.setInt(1, _ownerId);
			ps.setLong(2, _count);
			ps.setString(3, _loc.name());
			ps.setInt(4, _locData);
			ps.setInt(5, _enchantLevel);
			ps.setInt(6, _type1);
			ps.setInt(7, _type2);
			ps.setInt(8, _mana);
			ps.setLong(9, _time);
			ps.setInt(10, getObjectId());
			ps.executeUpdate();
			_existsInDb = true;
			_storedInDb = true;
			
			if (_augmentation != null)
			{
				updateItemOptions(con);
			}
			
			if (_elementals != null)
			{
				updateItemElements(con);
			}
			
			updateSpecialAbilities(con);
		}
		catch (Exception e)
		{
			LOGGER.Error("Item could not update " + this + " in DB: Reason: " + e);
		}
	}
	
	/**
	 * Insert the item in database
	 */
	private void insertIntoDb()
	{
		if (_existsInDb || (getObjectId() == 0) || _wear)
		{
			return;
		}
		
		try 
		{
		using GameServerDbContext ctx = new();
		PreparedStatement ps =
			con.prepareStatement(
				"INSERT INTO items (owner_id,item_id,count,loc,loc_data,enchant_level,object_id,custom_type1,custom_type2,mana_left,time) VALUES (?,?,?,?,?,?,?,?,?,?,?)");
			ps.setInt(1, _ownerId);
			ps.setInt(2, _itemId);
			ps.setLong(3, _count);
			ps.setString(4, _loc.name());
			ps.setInt(5, _locData);
			ps.setInt(6, _enchantLevel);
			ps.setInt(7, getObjectId());
			ps.setInt(8, _type1);
			ps.setInt(9, _type2);
			ps.setInt(10, _mana);
			ps.setLong(11, _time);
			
			ps.executeUpdate();
			_existsInDb = true;
			_storedInDb = true;
			
			if (_augmentation != null)
			{
				updateItemOptions(con);
			}
			
			if (_elementals != null)
			{
				updateItemElements(con);
			}
			
			updateSpecialAbilities(con);
		}
		catch (Exception e)
		{
			LOGGER.Error("Item could not insert " + this + " into DB: Reason: " + e);
		}
	}
	
	/**
	 * Delete item from database
	 */
	private void removeFromDb()
	{
		if (!_existsInDb || _wear)
		{
			return;
		}
		
		try
		{
			using GameServerDbContext ctx = new();

			{
				PreparedStatement ps = con.prepareStatement("DELETE FROM items WHERE object_id = ?");
				ps.setInt(1, getObjectId());
				ps.executeUpdate();
			}

			{
				PreparedStatement ps = con.prepareStatement("DELETE FROM item_variations WHERE itemId = ?");
				ps.setInt(1, getObjectId());
				ps.executeUpdate();
			}

			{
				PreparedStatement ps = con.prepareStatement("DELETE FROM item_elementals WHERE itemId = ?");
				ps.setInt(1, getObjectId());
				ps.executeUpdate();
			}

			{
				PreparedStatement ps = con.prepareStatement("DELETE FROM item_special_abilities WHERE objectId = ?");
				ps.setInt(1, getObjectId());
				ps.executeUpdate();
			}

			{
				PreparedStatement ps = con.prepareStatement("DELETE FROM item_variables WHERE id = ?");
				ps.setInt(1, getObjectId());
				ps.executeUpdate();
			}
		}
		catch (Exception e)
		{
			LOGGER.Error("Item could not delete " + this + " in DB " + e);
		}
		finally
		{
			_existsInDb = false;
			_storedInDb = false;
		}
	}
	
	public void resetOwnerTimer()
	{
		if (_itemLootShedule != null)
		{
			_itemLootShedule.cancel(true);
			_itemLootShedule = null;
		}
	}
	
	public void setItemLootShedule(ScheduledFuture<?> sf)
	{
		_itemLootShedule = sf;
	}
	
	public ScheduledFuture<?> getItemLootShedule()
	{
		return _itemLootShedule;
	}
	
	public void setProtected(bool isProtected)
	{
		_protected = isProtected;
	}
	
	public bool isProtected()
	{
		return _protected;
	}
	
	public bool isAvailable()
	{
		if (!_itemTemplate.isConditionAttached())
		{
			return true;
		}
		if ((_loc == ItemLocation.PET) || (_loc == ItemLocation.PET_EQUIP))
		{
			return true;
		}
		
		Player player = getActingPlayer();
		if (player != null)
		{
			foreach (Condition condition in _itemTemplate.getConditions())
			{
				if (condition == null)
				{
					continue;
				}
				
				if (!condition.testImpl(player, player, null, _itemTemplate))
				{
					return false;
				}
			}
			
			if (player.hasRequest<AutoPeelRequest>())
			{
				EtcItem etcItem = getEtcItem();
				if ((etcItem != null) && (etcItem.getExtractableItems() != null))
				{
					return false;
				}
			}
		}
		
		return true;
	}
	
	public void setCountDecrease(bool decrease)
	{
		_decrease = decrease;
	}
	
	public bool getCountDecrease()
	{
		return _decrease;
	}
	
	public void setInitCount(int initCount)
	{
		_initCount = initCount;
	}
	
	public long getInitCount()
	{
		return _initCount;
	}
	
	public void restoreInitCount()
	{
		if (_decrease)
		{
			setCount(_initCount);
		}
	}
	
	public bool isTimeLimitedItem()
	{
		return _time > 0;
	}
	
	/**
	 * Returns (current system time + time) of this time limited item
	 * @return Time
	 */
	public long getTime()
	{
		return _time;
	}
	
	public long getRemainingTime()
	{
		return _time - System.currentTimeMillis();
	}
	
	public void endOfLife()
	{
		Player player = getActingPlayer();
		if (player != null)
		{
			if (isEquipped())
			{
				InventoryUpdate iu = new InventoryUpdate();
				foreach (Item item in player.getInventory().unEquipItemInSlotAndRecord(getLocationSlot()))
				{
					iu.addModifiedItem(item);
				}
				player.sendInventoryUpdate(iu);
			}
			
			if (_loc != ItemLocation.WAREHOUSE)
			{
				// destroy
				player.getInventory().destroyItem("Item", this, player, null);
				
				// send update
				InventoryUpdate iu = new InventoryUpdate();
				iu.addRemovedItem(this);
				player.sendInventoryUpdate(iu);
			}
			else
			{
				player.getWarehouse().destroyItem("Item", this, player, null);
			}
			player.sendPacket(new SystemMessage(SystemMessageId.S1_HAS_EXPIRED).addItemName(_itemId));
		}
	}
	
	public void scheduleLifeTimeTask()
	{
		if (!isTimeLimitedItem())
		{
			return;
		}
		if (getRemainingTime() <= 0)
		{
			endOfLife();
		}
		else
		{
			ItemLifeTimeTaskManager.getInstance().add(this, getTime());
		}
	}
	
	public void setDropperObjectId(int id)
	{
		_dropperObjectId = id;
	}
	
	public override void sendInfo(Player player)
	{
		if (_dropperObjectId != 0)
		{
			player.sendPacket(new DropItem(this, _dropperObjectId));
		}
		else
		{
			player.sendPacket(new SpawnItem(this));
		}
	}
	
	public DropProtection getDropProtection()
	{
		return _dropProtection;
	}
	
	public bool isPublished()
	{
		return _published;
	}
	
	public void publish()
	{
		_published = true;
	}
	
	public override bool decayMe()
	{
		if (Config.SAVE_DROPPED_ITEM)
		{
			ItemsOnGroundManager.getInstance().removeObject(this);
		}
		
		return base.decayMe();
	}
	
	public bool isQuestItem()
	{
		return _itemTemplate.isQuestItem();
	}
	
	public bool isElementable()
	{
		if ((_loc == ItemLocation.INVENTORY) || (_loc == ItemLocation.PAPERDOLL))
		{
			return _itemTemplate.isElementable();
		}
		return false;
	}
	
	public bool isFreightable()
	{
		return _itemTemplate.isFreightable();
	}
	
	public int useSkillDisTime()
	{
		return _itemTemplate.useSkillDisTime();
	}
	
	public int getOlyEnchantLevel()
	{
		Player player = getActingPlayer();
		int enchant = _enchantLevel;
		
		if (player == null)
		{
			return enchant;
		}
		
		if (player.isInOlympiadMode())
		{
			if (_itemTemplate.isWeapon())
			{
				if ((Config.ALT_OLY_WEAPON_ENCHANT_LIMIT >= 0) && (enchant > Config.ALT_OLY_WEAPON_ENCHANT_LIMIT))
				{
					enchant = Config.ALT_OLY_WEAPON_ENCHANT_LIMIT;
				}
			}
			else
			{
				if ((Config.ALT_OLY_ARMOR_ENCHANT_LIMIT >= 0) && (enchant > Config.ALT_OLY_ARMOR_ENCHANT_LIMIT))
				{
					enchant = Config.ALT_OLY_ARMOR_ENCHANT_LIMIT;
				}
			}
		}
		
		return enchant;
	}
	
	public bool hasPassiveSkills()
	{
		return (_itemTemplate.getItemType() == EtcItemType.ENCHT_ATTR_RUNE) && (_loc == ItemLocation.INVENTORY) && (_ownerId > 0) && (_itemTemplate.getSkills(ItemSkillType.NORMAL) != null);
	}
	
	public void giveSkillsToOwner()
	{
		if (!hasPassiveSkills())
		{
			return;
		}
		
		Player player = getActingPlayer();
		if (player != null)
		{
			_itemTemplate.forEachSkill(ItemSkillType.NORMAL, holder =>
			{
				Skill skill = holder.getSkill();
				if (skill.isPassive())
				{
					player.addSkill(skill, false);
				}
			});
		}
	}
	
	public void removeSkillsFromOwner()
	{
		if (!hasPassiveSkills())
		{
			return;
		}
		
		Player player = getActingPlayer();
		if (player != null)
		{
			_itemTemplate.forEachSkill(ItemSkillType.NORMAL, holder =>
			{
				Skill skill = holder.getSkill();
				if (skill.isPassive())
				{
					player.removeSkill(skill, false, skill.isPassive());
				}
			});
		}
	}
	
	public override bool isItem()
	{
		return true;
	}
	
	public override Player getActingPlayer()
	{
		if ((_owner == null) && (_ownerId != 0))
		{
			_owner = World.getInstance().getPlayer(_ownerId);
		}
		return _owner;
	}
	
	public int getEquipReuseDelay()
	{
		return _itemTemplate.getEquipReuseDelay();
	}
	
	/**
	 * @param player
	 * @param command
	 */
	public void onBypassFeedback(Player player, String command)
	{
		if (command.StartsWith("Quest"))
		{
			String questName = command.Substring(6);
			String @event = null;
			int idx = questName.IndexOf(' ');
			if (idx > 0)
			{
				@event = questName.Substring(idx).Trim();
			}
			
			if (@event != null)
			{
				if (EventDispatcher.getInstance().hasListener(EventType.ON_ITEM_BYPASS_EVENT, getTemplate()))
				{
					EventDispatcher.getInstance().notifyEventAsync(new OnItemBypassEvent(this, player, @event), getTemplate());
				}
			}
			else if (EventDispatcher.getInstance().hasListener(EventType.ON_ITEM_TALK, getTemplate()))
			{
				EventDispatcher.getInstance().notifyEventAsync(new OnItemTalk(this, player), getTemplate());
			}
		}
	}
	
	/**
	 * Returns enchant effect object for this item
	 * @return enchanteffect
	 */
	public int[] getEnchantOptions()
	{
		EnchantOptions op = EnchantItemOptionsData.getInstance().getOptions(this);
		if (op != null)
		{
			return op.getOptions();
		}
		return DEFAULT_ENCHANT_OPTIONS;
	}
	
	public ICollection<EnsoulOption> getSpecialAbilities()
	{
		List<EnsoulOption> result = new();
		foreach (EnsoulOption ensoulOption in _ensoulOptions)
		{
			if (ensoulOption != null)
			{
				result.Add(ensoulOption);
			}
		}
		return result;
	}
	
	public EnsoulOption getSpecialAbility(int index)
	{
		return _ensoulOptions[index];
	}
	
	public ICollection<EnsoulOption> getAdditionalSpecialAbilities()
	{
		List<EnsoulOption> result = new();
		foreach (EnsoulOption ensoulSpecialOption in _ensoulSpecialOptions)
		{
			if (ensoulSpecialOption != null)
			{
				result.Add(ensoulSpecialOption);
			}
		}
		return result;
	}
	
	public EnsoulOption getAdditionalSpecialAbility(int index)
	{
		return _ensoulSpecialOptions[index];
	}
	
	public void addSpecialAbility(EnsoulOption option, int position, int type, bool updateInDB)
	{
		if ((type == 1) && ((position < 0) || (position > 1))) // two first slots
		{
			return;
		}
		if ((type == 2) && (position != 0)) // third slot
		{
			return;
		}
		
		if (type == 1) // Adding regular ability
		{
			EnsoulOption oldOption = _ensoulOptions[position];
			if (oldOption != null)
			{
				removeSpecialAbility(oldOption);
			}
			if (position < _itemTemplate.getEnsoulSlots())
			{
				_ensoulOptions[position] = option;
			}
		}
		else if (type == 2) // Adding special ability
		{
			EnsoulOption oldOption = _ensoulSpecialOptions[position];
			if (oldOption != null)
			{
				removeSpecialAbility(oldOption);
			}
			if (position < _itemTemplate.getSpecialEnsoulSlots())
			{
				_ensoulSpecialOptions[position] = option;
			}
		}
		
		if (updateInDB)
		{
			updateSpecialAbilities();
		}
	}
	
	public void removeSpecialAbility(int position, int type)
	{
		if (type == 1)
		{
			EnsoulOption option = _ensoulOptions[position];
			if (option != null)
			{
				removeSpecialAbility(option);
				_ensoulOptions[position] = null;
				
				// Rearrange.
				if (position == 0)
				{
					EnsoulOption secondEnsoul = _ensoulOptions[1];
					if (secondEnsoul != null)
					{
						removeSpecialAbility(secondEnsoul);
						_ensoulOptions[1] = null;
						addSpecialAbility(secondEnsoul, 0, type, true);
					}
				}
			}
		}
		else if (type == 2)
		{
			EnsoulOption option = _ensoulSpecialOptions[position];
			if (option != null)
			{
				removeSpecialAbility(option);
				_ensoulSpecialOptions[position] = null;
			}
		}
	}
	
	public void clearSpecialAbilities()
	{
		foreach (EnsoulOption ensoulOption in _ensoulOptions)
		{
			clearSpecialAbility(ensoulOption);
		}
		foreach (EnsoulOption ensoulSpecialOption in _ensoulSpecialOptions)
		{
			clearSpecialAbility(ensoulSpecialOption);
		}
	}
	
	public void applySpecialAbilities()
	{
		if (!isEquipped())
		{
			return;
		}
		
		foreach (EnsoulOption ensoulOption in _ensoulOptions)
		{
			applySpecialAbility(ensoulOption);
		}
		foreach (EnsoulOption ensoulSpecialOption in _ensoulSpecialOptions)
		{
			applySpecialAbility(ensoulSpecialOption);
		}
	}
	
	private void removeSpecialAbility(EnsoulOption option)
	{
		try 
		{
			using GameServerDbContext ctx = new();
			PreparedStatement ps =
				con.prepareStatement("DELETE FROM item_special_abilities WHERE objectId = ? AND optionId = ?");
			ps.setInt(1, getObjectId());
			ps.setInt(2, option.getId());
			ps.execute();
			
			Skill skill = option.getSkill();
			if (skill != null)
			{
				Player player = getActingPlayer();
				if (player != null)
				{
					player.removeSkill(skill.getId());
				}
			}
		}
		catch (Exception e)
		{
			LOGGER.Warn("Item could not remove special ability for " + this + ": " + e);
		}
	}
	
	private void applySpecialAbility(EnsoulOption option)
	{
		if (option == null)
		{
			return;
		}
		
		Skill skill = option.getSkill();
		if (skill != null)
		{
			Player player = getActingPlayer();
			if ((player != null) && (player.getSkillLevel(skill.getId()) != skill.getLevel()))
			{
				player.addSkill(skill, false);
			}
		}
	}
	
	private void clearSpecialAbility(EnsoulOption option)
	{
		if (option == null)
		{
			return;
		}
		
		Skill skill = option.getSkill();
		if (skill != null)
		{
			Player player = getActingPlayer();
			if (player != null)
			{
				player.removeSkill(skill, false, true);
			}
		}
	}
	
	private void restoreSpecialAbilities()
	{
		try 
		{
			using GameServerDbContext ctx = new();
			PreparedStatement ps =
				con.prepareStatement("SELECT * FROM item_special_abilities WHERE objectId = ? ORDER BY position");
			ps.setInt(1, getObjectId());
			{
				ResultSet rs = ps.executeQuery();
				while (rs.next())
				{
					int optionId = rs.getInt("optionId");
					int type = rs.getInt("type");
					int position = rs.getInt("position");
					EnsoulOption option = EnsoulData.getInstance().getOption(optionId);
					if (option != null)
					{
						addSpecialAbility(option, position, type, false);
					}
				}
			}
		}
		catch (Exception e)
		{
			LOGGER.Warn("Item could not restore special abilities for " + this + ": " + e);
		}
	}
	
	public void updateSpecialAbilities()
	{
		try
		{
			using GameServerDbContext ctx = new();
			updateSpecialAbilities(con);
		}
		catch (Exception e)
		{
			LOGGER.Warn("Item could not update item special abilities: " + e);
		}
	}
	
	private void updateSpecialAbilities(Connection con)
	{
		try
		{
			PreparedStatement ps = con.prepareStatement(
				"INSERT INTO item_special_abilities (`objectId`, `type`, `optionId`, `position`) VALUES (?, ?, ?, ?) ON DUPLICATE KEY UPDATE type = ?, optionId = ?, position = ?");
			ps.setInt(1, getObjectId());
			for (int i = 0; i < _ensoulOptions.Length; i++)
			{
				if (_ensoulOptions[i] == null)
				{
					continue;
				}
				
				ps.setInt(2, 1); // regular options
				ps.setInt(3, _ensoulOptions[i].getId());
				ps.setInt(4, i);
				
				ps.setInt(5, 1); // regular options
				ps.setInt(6, _ensoulOptions[i].getId());
				ps.setInt(7, i);
				ps.execute();
			}
			
			for (int i = 0; i < _ensoulSpecialOptions.Length; i++)
			{
				if (_ensoulSpecialOptions[i] == null)
				{
					continue;
				}
				
				ps.setInt(2, 2); // special options
				ps.setInt(3, _ensoulSpecialOptions[i].getId());
				ps.setInt(4, i);
				
				ps.setInt(5, 2); // special options
				ps.setInt(6, _ensoulSpecialOptions[i].getId());
				ps.setInt(7, i);
				ps.execute();
			}
		}
		catch (Exception e)
		{
			LOGGER.Warn("Item could not update item special abilities: " + e);
		}
	}
	
	/**
	 * Clears all the enchant bonuses if item is enchanted and containing bonuses for enchant value.
	 */
	public void clearEnchantStats()
	{
		Player player = getActingPlayer();
		if (player == null)
		{
			_enchantOptions.Clear();
			return;
		}
		
		foreach (Options op in _enchantOptions)
		{
			op.remove(player);
		}
		_enchantOptions.Clear();
	}
	
	/**
	 * Clears and applies all the enchant bonuses if item is enchanted and containing bonuses for enchant value.
	 */
	public void applyEnchantStats()
	{
		Player player = getActingPlayer();
		if (!isEquipped() || (player == null) || (getEnchantOptions() == DEFAULT_ENCHANT_OPTIONS))
		{
			return;
		}
		
		foreach (int id in getEnchantOptions())
		{
			Options options = OptionData.getInstance().getOptions(id);
			if (options != null)
			{
				options.apply(player);
				_enchantOptions.Add(options);
			}
			else if (id != 0)
			{
				LOGGER.Info("Item applyEnchantStats could not find option " + id + " " + this + " " + player);
			}
		}
	}
	
	public override void setHeading(int heading)
	{
	}
	
	public void stopAllTasks()
	{
		ItemLifeTimeTaskManager.getInstance().remove(this);
		ItemAppearanceTaskManager.getInstance().remove(this);
	}
	
	public ItemVariables getVariables()
	{
		ItemVariables vars = getScript(ItemVariables.class);
		return vars != null ? vars : addScript(new ItemVariables(getObjectId()));
	}
	
	public int getVisualId()
	{
		int visualId = getVariables().getInt(ItemVariables.VISUAL_ID, 0);
		if (visualId > 0)
		{
			int appearanceStoneId = getVariables().getInt(ItemVariables.VISUAL_APPEARANCE_STONE_ID, 0);
			if (appearanceStoneId > 0)
			{
				AppearanceStone stone = AppearanceItemData.getInstance().getStone(appearanceStoneId);
				if (stone != null)
				{
					Player player = getActingPlayer();
					if (player != null)
					{
						if (!stone.getRaces().isEmpty() && !stone.getRaces().contains(player.getRace()))
						{
							return 0;
						}
						if (!stone.getRacesNot().isEmpty() && stone.getRacesNot().contains(player.getRace()))
						{
							return 0;
						}
					}
				}
			}
		}
		return visualId;
	}
	
	public void setVisualId(int visualId)
	{
		setVisualId(visualId, true);
	}
	
	public void setVisualId(int visualId, bool announce)
	{
		getVariables().set(ItemVariables.VISUAL_ID, visualId);
		
		// When removed, cancel existing lifetime task.
		if (visualId == 0)
		{
			ItemAppearanceTaskManager.getInstance().remove(this);
			onVisualLifeTimeEnd(announce);
		}
	}
	
	public int getAppearanceStoneId()
	{
		return getVariables().getInt(ItemVariables.VISUAL_APPEARANCE_STONE_ID, 0);
	}
	
	public long getVisualLifeTime()
	{
		return getVariables().getLong(ItemVariables.VISUAL_APPEARANCE_LIFE_TIME, 0);
	}
	
	public void scheduleVisualLifeTime()
	{
		ItemAppearanceTaskManager.getInstance().remove(this);
		if (getVisualLifeTime() > 0)
		{
			long endTime = getVisualLifeTime();
			if ((endTime - System.currentTimeMillis()) > 0)
			{
				ItemAppearanceTaskManager.getInstance().add(this, endTime);
			}
			else
			{
				onVisualLifeTimeEnd();
			}
		}
	}
	
	public void onVisualLifeTimeEnd()
	{
		onVisualLifeTimeEnd(true);
	}
	
	public void onVisualLifeTimeEnd(bool announce)
	{
		removeVisualSetSkills();
		
		ItemVariables vars = getVariables();
		vars.remove(ItemVariables.VISUAL_ID);
		vars.remove(ItemVariables.VISUAL_APPEARANCE_STONE_ID);
		vars.remove(ItemVariables.VISUAL_APPEARANCE_LIFE_TIME);
		vars.storeMe();
		
		Player player = getActingPlayer();
		if (player != null)
		{
			InventoryUpdate iu = new InventoryUpdate();
			iu.addModifiedItem(this);
			player.broadcastUserInfo(UserInfoType.APPAREANCE);
			player.sendInventoryUpdate(iu);
			
			if (announce)
			{
				if (isEnchanted())
				{
					player.sendPacket(new SystemMessage(SystemMessageId.S1_S2_THE_ITEM_S_TEMPORARY_APPEARANCE_HAS_BEEN_RESET).addInt(_enchantLevel).addItemName(this));
				}
				else
				{
					player.sendPacket(new SystemMessage(SystemMessageId.S1_THE_ITEM_S_TEMPORARY_APPEARANCE_HAS_BEEN_RESET).addItemName(this));
				}
			}
		}
	}
	
	public bool isBlessed()
	{
		return _isBlessed;
	}
	
	public void setBlessed(bool blessed)
	{
		_isBlessed = blessed;
		
		ItemVariables vars = getVariables();
		if (!blessed)
		{
			vars.remove(ItemVariables.BLESSED);
		}
		else
		{
			vars.set(ItemVariables.BLESSED, true);
		}
		vars.storeMe();
	}
	
	public void removeVisualSetSkills()
	{
		if (!isEquipped())
		{
			return;
		}
		
		int appearanceStoneId = getAppearanceStoneId();
		if (appearanceStoneId > 0)
		{
			AppearanceStone stone = AppearanceItemData.getInstance().getStone(appearanceStoneId);
			if ((stone != null) && (stone.getType() == AppearanceType.FIXED))
			{
				Player player = getActingPlayer();
				if (player != null)
				{
					bool update = false;
					foreach (ArmorSet armorSet in ArmorSetData.getInstance().getSets(stone.getVisualId()))
					{
						if ((armorSet.getPiecesCount(player, Item::getVisualId) - 1 /* not removed yet */) < armorSet.getMinimumPieces())
						{
							foreach (ArmorsetSkillHolder holder in armorSet.getSkills())
							{
								Skill skill = holder.getSkill();
								if (skill != null)
								{
									player.removeSkill(skill, false, skill.isPassive());
									update = true;
								}
							}
						}
					}
					
					if (update)
					{
						player.sendSkillList();
					}
				}
			}
		}
	}
	
	public void applyVisualSetSkills()
	{
		if (!isEquipped())
		{
			return;
		}
		
		int appearanceStoneId = getAppearanceStoneId();
		if (appearanceStoneId > 0)
		{
			AppearanceStone stone = AppearanceItemData.getInstance().getStone(appearanceStoneId);
			if ((stone != null) && (stone.getType() == AppearanceType.FIXED))
			{
				Player player = getActingPlayer();
				if (player != null)
				{
					bool update = false;
					bool updateTimeStamp = false;
					foreach (ArmorSet armorSet in ArmorSetData.getInstance().getSets(stone.getVisualId()))
					{
						if (armorSet.getPiecesCount(player, Item::getVisualId) >= armorSet.getMinimumPieces())
						{
							foreach (ArmorsetSkillHolder holder in armorSet.getSkills())
							{
								if (player.getSkillLevel(holder.getSkillId()) >= holder.getSkillLevel())
								{
									continue;
								}
								
								Skill skill = holder.getSkill();
								if ((skill == null) || (skill.isPassive() && !skill.checkConditions(SkillConditionScope.PASSIVE, player, player)))
								{
									continue;
								}
								
								player.addSkill(skill, false);
								update = true;
								
								if (skill.isActive())
								{
									if (!player.hasSkillReuse(skill.getReuseHashCode()))
									{
										int equipDelay = getEquipReuseDelay();
										if (equipDelay > 0)
										{
											player.addTimeStamp(skill, equipDelay);
											player.disableSkill(skill, equipDelay);
										}
									}
									
									// Active, non offensive, skills start with reuse on equip.
									if (!skill.isBad() && !skill.isTransformation() && (Config.ARMOR_SET_EQUIP_ACTIVE_SKILL_REUSE > 0) && player.hasEnteredWorld())
									{
										player.addTimeStamp(skill, skill.getReuseDelay() > 0 ? skill.getReuseDelay() : Config.ARMOR_SET_EQUIP_ACTIVE_SKILL_REUSE);
									}
									
									updateTimeStamp = true;
								}
							}
						}
					}
					
					if (updateTimeStamp)
					{
						player.sendPacket(new SkillCoolTime(player));
					}
					
					if (update)
					{
						player.sendSkillList();
					}
				}
			}
		}
	}
	
	/**
	 * Returns the item in String format
	 * @return String
	 */
	public override String toString()
	{
		StringBuilder sb = new StringBuilder();
		sb.Append(_itemTemplate);
		sb.Append("[");
		sb.Append(getObjectId());
		sb.Append("]");
		return sb.ToString();
	}
}