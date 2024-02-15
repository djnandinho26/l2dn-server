using System.Xml.Linq;
using L2Dn.Extensions;
using L2Dn.GameServer.Db;
using L2Dn.GameServer.Enums;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Items.Instances;
using L2Dn.GameServer.Network.OutgoingPackets;
using L2Dn.GameServer.Utilities;
using L2Dn.Utilities;
using NLog;

namespace L2Dn.GameServer.Data.Xml;

/**
 * This class holds the Initial Shortcuts information.<br>
 * What shortcuts get each newly created character.
 * @author Zoey76
 */
public class InitialShortcutData: DataReaderBase
{
	private static readonly Logger LOGGER = LogManager.GetLogger(nameof(InitialShortcutData));
	
	private readonly Map<CharacterClass, List<Shortcut>> _initialShortcutData = new();
	private readonly List<Shortcut> _initialGlobalShortcutList = new();
	private readonly Map<int, Macro> _macroPresets = new();
	
	/**
	 * Instantiates a new initial shortcuts data.
	 */
	protected InitialShortcutData()
	{
		load();
	}
	
	public void load()
	{
		_initialShortcutData.clear();
		_initialGlobalShortcutList.Clear();
		
		XDocument document = LoadXmlDocument(DataFileLocation.Data, "stats/initialShortcuts.xml");
		document.Elements("list").Elements("shortcuts").ForEach(parseShortcut);
		document.Elements("list").Elements("macros").Elements("macro").ForEach(parseMacro);

		LOGGER.Info(GetType().Name + ": Loaded " + _initialGlobalShortcutList.size() + " initial global shortcuts data.");
		LOGGER.Info(GetType().Name + ": Loaded " + _initialShortcutData.size() + " initial shortcuts data.");
		LOGGER.Info(GetType().Name + ": Loaded " + _macroPresets.size() + " macro presets.");
	}
	
	/**
	 * Parses a shortcut.
	 * @param d the node
	 */
	private void parseShortcut(XElement element)
	{
		int classId = element.Attribute("classId").GetInt32(-1);
		List<Shortcut> list = new();
		
		element.Elements("page").ForEach(pageElement =>
		{
			int pageId = pageElement.Attribute("pageId").GetInt32();
			pageElement.Elements("slot").ForEach(slotElement =>
			{
				int slotId = slotElement.Attribute("slotId").GetInt32();
				ShortcutType shortcutType = slotElement.Attribute("shortcutType").GetEnum<ShortcutType>();
				int shortcutId = slotElement.Attribute("shortcutId").GetInt32();
				int shortcutLevel = slotElement.Attribute("shortcutLevel").GetInt32(0);
				int characterType = slotElement.Attribute("characterType").GetInt32(0);
				Shortcut shortcut = new Shortcut(slotId, pageId, shortcutType, shortcutId, shortcutLevel, 0, characterType);
				list.add(shortcut);
			});
		});
		
		if (classId < 0)
			_initialGlobalShortcutList.AddRange(list);
		else
			_initialShortcutData.put((CharacterClass)classId, list);
	}

	/**
	 * Parses a macro.
	 * @param d the node
	 */
	private void parseMacro(XElement element)
	{
		bool enabled = element.Attribute("enabled").GetBoolean(true);
		if (!enabled)
			return;

		int macroId = element.Attribute("macroId").GetInt32();
		int icon = element.Attribute("icon").GetInt32();
		String name = element.Attribute("name").GetString();
		String description = element.Attribute("description").GetString();
		String acronym = element.Attribute("acronym").GetString();
		List<MacroCmd> commands = new();
		int entry = 0;

		element.Elements("command").ForEach(el =>
		{
			MacroType type = el.Attribute("type").GetEnum<MacroType>();
			int d1 = 0;
			int d2 = 0;
			string cmd = (string)el;
			switch (type)
			{
				case MacroType.SKILL:
				{
					d1 = el.Attribute("skillId").GetInt32(); // Skill ID
					d2 = el.Attribute("skillLevel").GetInt32(0); // Skill level
					break;
				}
				case MacroType.ACTION:
				{
					// Not handled by client.
					d1 = el.Attribute("actionId").GetInt32();
					break;
				}
				case MacroType.TEXT:
				{
					// Doesn't have numeric parameters.
					break;
				}
				case MacroType.SHORTCUT:
				{
					d1 = el.Attribute("page").GetInt32(); // Page
					d2 = el.Attribute("slot").GetInt32(0); // Slot
					break;
				}
				case MacroType.ITEM:
				{
					// Not handled by client.
					d1 = el.Attribute("itemId").GetInt32();
					break;
				}
				case MacroType.DELAY:
				{
					d1 = el.Attribute("delay").GetInt32(); // Delay in seconds
					break;
				}
			}

			commands.add(new MacroCmd(entry++, type, d1, d2, cmd));
		});

		_macroPresets.put(macroId, new Macro(macroId, icon, name, description, acronym, commands));
	}
	
	/**
	 * Gets the shortcut list.
	 * @param cId the class ID for the shortcut list
	 * @return the shortcut list for the give class ID
	 */
	public List<Shortcut> getShortcutList(CharacterClass cId)
	{
		return _initialShortcutData.get(cId);
	}
	
	/**
	 * Gets the global shortcut list.
	 * @return the global shortcut list
	 */
	public List<Shortcut> getGlobalMacroList()
	{
		return _initialGlobalShortcutList;
	}
	
	/**
	 * Register all the available shortcuts for the given player.
	 * @param player the player
	 */
	public void registerAllShortcuts(Player player)
	{
		if (player == null)
		{
			return;
		}
		
		// Register global shortcuts.
		foreach (Shortcut shortcut in _initialGlobalShortcutList)
		{
			int shortcutId = shortcut.getId();
			switch (shortcut.getType())
			{
				case ShortcutType.ITEM:
				{
					Item item = player.getInventory().getItemByItemId(shortcutId);
					if (item == null)
					{
						continue;
					}
					shortcutId = item.getObjectId();
					break;
				}
				case ShortcutType.SKILL:
				{
					if (!player.getSkills().containsKey(shortcutId))
					{
						continue;
					}
					break;
				}
				case ShortcutType.MACRO:
				{
					Macro macro = _macroPresets.get(shortcutId);
					if (macro == null)
					{
						continue;
					}
					player.registerMacro(macro);
					break;
				}
			}
			
			// Register shortcut
			Shortcut newShortcut = new Shortcut(shortcut.getSlot(), shortcut.getPage(), shortcut.getType(), shortcutId, shortcut.getLevel(), shortcut.getSubLevel(), shortcut.getCharacterType());
			player.sendPacket(new ShortCutRegisterPacket(newShortcut, player));
			player.registerShortCut(newShortcut);
		}
		
		// Register class specific shortcuts.
		if (_initialShortcutData.containsKey(player.getClassId()))
		{
			foreach (Shortcut shortcut in _initialShortcutData.get(player.getClassId()))
			{
				int shortcutId = shortcut.getId();
				switch (shortcut.getType())
				{
					case ShortcutType.ITEM:
					{
						Item item = player.getInventory().getItemByItemId(shortcutId);
						if (item == null)
						{
							continue;
						}
						shortcutId = item.getObjectId();
						break;
					}
					case ShortcutType.SKILL:
					{
						if (!player.getSkills().containsKey(shortcut.getId()))
						{
							continue;
						}
						break;
					}
					case ShortcutType.MACRO:
					{
						Macro macro = _macroPresets.get(shortcutId);
						if (macro == null)
						{
							continue;
						}
						player.registerMacro(macro);
						break;
					}
				}
				// Register shortcut
				Shortcut newShortcut = new Shortcut(shortcut.getSlot(), shortcut.getPage(), shortcut.getType(), shortcutId, shortcut.getLevel(), shortcut.getSubLevel(), shortcut.getCharacterType());
				player.sendPacket(new ShortCutRegisterPacket(newShortcut, player));
				player.registerShortCut(newShortcut);
			}
		}
	}
	
	/**
	 * Gets the single instance of InitialEquipmentData.
	 * @return single instance of InitialEquipmentData
	 */
	public static InitialShortcutData getInstance()
	{
		return SingletonHolder.INSTANCE;
	}
	
	private static class SingletonHolder
	{
		public static readonly InitialShortcutData INSTANCE = new();
	}
}