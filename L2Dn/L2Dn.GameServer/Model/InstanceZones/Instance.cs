using L2Dn.GameServer.Enums;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Actor.Instances;
using L2Dn.GameServer.Model.Actor.Templates;
using L2Dn.GameServer.Model.Events;
using L2Dn.GameServer.Model.Events.Impl.Instances;
using L2Dn.GameServer.Model.Interfaces;
using L2Dn.GameServer.Utilities;
using NLog;
using ThreadPool = System.Threading.ThreadPool;

namespace L2Dn.GameServer.Model.InstanceZones;

/**
 * Instance world.
 * @author malyelfik
 */
public class Instance : IIdentifiable, INamable
{
	private static readonly Logger LOGGER = LogManager.GetLogger(nameof(Instance)));
	
	// Basic instance parameters
	private readonly int _id;
	private readonly InstanceTemplate _template;
	private readonly long _startTime;
	private long _endTime;
	// Advanced instance parameters
	private readonly Set<int> _allowed = new(); // Player ids which can enter to instance
	private readonly Set<Player> _players = new(); // Players inside instance
	private readonly Set<Npc> _npcs = new(); // Spawned NPCs inside instance
	private readonly Map<int, Door> _doors = new(); // Spawned doors inside instance
	private readonly StatSet _parameters = new StatSet();
	// Timers
	private readonly Map<int, ScheduledFuture<?>> _ejectDeadTasks = new();
	private ScheduledFuture<?> _cleanUpTask = null;
	private ScheduledFuture<?> _emptyDestroyTask = null;
	private readonly List<SpawnTemplate> _spawns;
	
	/**
	 * Create instance world.
	 * @param id ID of instance world
	 * @param template template of instance world
	 * @param player player who create instance world.
	 */
	public Instance(int id, InstanceTemplate template, Player player)
	{
		// Set basic instance info
		_id = id;
		_template = template;
		_startTime = System.currentTimeMillis();
		_spawns = new(template.getSpawns().size());
		
		// Clone and add the spawn templates
		foreach (SpawnTemplate spawn in template.getSpawns())
		{
			_spawns.Add(spawn.clone());
		}
		
		// Register world to instance manager.
		InstanceManager.getInstance().register(this);
		
		// Set duration, spawns, status, etc..
		setDuration(_template.getDuration());
		setStatus(0);
		spawnDoors();
		
		// Initialize instance spawns.
		foreach (SpawnTemplate spawnTemplate in _spawns)
		{
			if (spawnTemplate.isSpawningByDefault())
			{
				spawnTemplate.spawnAll(this);
			}
		}
		
		// Notify DP scripts
		if (!isDynamic() && EventDispatcher.getInstance().hasListener(EventType.ON_INSTANCE_CREATED, _template))
		{
			EventDispatcher.getInstance().notifyEventAsync(new OnInstanceCreated(this, player), _template);
		}
	}
	
	public int getId()
	{
		return _id;
	}
	
	public String getName()
	{
		return _template.getName();
	}
	
	/**
	 * Check if instance has been created dynamically or have XML template.
	 * @return {@code true} if instance is dynamic or {@code false} if instance has static template
	 */
	public bool isDynamic()
	{
		return _template.getId() == -1;
	}
	
	/**
	 * Set instance world parameter.
	 * @param key parameter name
	 * @param value parameter value
	 */
	public void setParameter(String key, Object value)
	{
		if (value == null)
		{
			_parameters.remove(key);
		}
		else
		{
			_parameters.set(key, value);
		}
	}
	
	/**
	 * Set instance world parameter.
	 * @param key parameter name
	 * @param value parameter value
	 */
	public void setParameter(String key, bool value)
	{
		_parameters.set(key, value ? Boolean.TRUE : Boolean.FALSE);
	}
	
	/**
	 * Get instance world parameters.
	 * @return instance parameters
	 */
	public StatSet getParameters()
	{
		return _parameters;
	}
	
	/**
	 * Get status of instance world.
	 * @return instance status, otherwise 0
	 */
	public int getStatus()
	{
		return _parameters.getInt("INSTANCE_STATUS", 0);
	}
	
	/**
	 * Check if instance status is equal to {@code status}.
	 * @param status number used for status comparison
	 * @return {@code true} when instance status and {@code status} are equal, otherwise {@code false}
	 */
	public bool isStatus(int status)
	{
		return getStatus() == status;
	}
	
	/**
	 * Set status of instance world.
	 * @param value new world status
	 */
	public void setStatus(int value)
	{
		_parameters.set("INSTANCE_STATUS", value);
		
		if (EventDispatcher.getInstance().hasListener(EventType.ON_INSTANCE_STATUS_CHANGE, _template))
		{
			EventDispatcher.getInstance().notifyEventAsync(new OnInstanceStatusChange(this, value), _template);
		}
	}
	
	/**
	 * Increment instance world status
	 * @return new world status
	 */
	public int incStatus()
	{
		int status = getStatus() + 1;
		setStatus(status);
		return status;
	}
	
	/**
	 * Add player who can enter to instance.
	 * @param player player instance
	 */
	public void addAllowed(Player player)
	{
		if (!_allowed.contains(player.getObjectId()))
		{
			_allowed.add(player.getObjectId());
		}
	}
	
	/**
	 * Check if player can enter to instance.
	 * @param player player itself
	 * @return {@code true} when can enter, otherwise {@code false}
	 */
	public bool isAllowed(Player player)
	{
		return _allowed.contains(player.getObjectId());
	}
	
	/**
	 * Returns all players who can enter to instance.
	 * @return allowed players list
	 */
	public List<Player> getAllowed()
	{
		List<Player> allowed = new(_allowed.size());
		foreach (int playerId in _allowed)
		{
			Player player = World.getInstance().getPlayer(playerId);
			if (player != null)
			{
				allowed.Add(player);
			}
		}
		return allowed;
	}
	
	/**
	 * Add player to instance
	 * @param player player instance
	 */
	public void addPlayer(Player player)
	{
		_players.add(player);
		if (_emptyDestroyTask != null)
		{
			_emptyDestroyTask.cancel(false);
			_emptyDestroyTask = null;
		}
	}
	
	/**
	 * Remove player from instance.
	 * @param player player instance
	 */
	public void removePlayer(Player player)
	{
		_players.remove(player);
		if (_players.isEmpty())
		{
			long emptyTime = _template.getEmptyDestroyTime();
			if ((_template.getDuration() == 0) || (emptyTime == 0))
			{
				destroy();
			}
			else if ((emptyTime >= 0) && (_emptyDestroyTask == null) && (getRemainingTime() < emptyTime))
			{
				_emptyDestroyTask = ThreadPool.schedule(this::destroy, emptyTime);
			}
		}
	}
	
	/**
	 * Check if player is inside instance.
	 * @param player player to be checked
	 * @return {@code true} if player is inside, otherwise {@code false}
	 */
	public bool containsPlayer(Player player)
	{
		return _players.contains(player);
	}
	
	/**
	 * Get all players inside instance.
	 * @return players within instance
	 */
	public Set<Player> getPlayers()
	{
		return _players;
	}
	
	/**
	 * Get count of players inside instance.
	 * @return players count inside instance
	 */
	public int getPlayersCount()
	{
		return _players.size();
	}
	
	/**
	 * Get first found player from instance world.<br>
	 * <i>This method is useful for instances with one player inside.</i>
	 * @return first found player, otherwise {@code null}
	 */
	public Player getFirstPlayer()
	{
		foreach (Player player in _players)
		{
			return player;
		}
		return null;
	}
	
	/**
	 * Get player by ID from instance.
	 * @param id objectId of player
	 * @return first player by ID, otherwise {@code null}
	 */
	public Player getPlayerById(int id)
	{
		foreach (Player player in _players)
		{
			if (player.getObjectId() == id)
			{
				return player;
			}
		}
		return null;
	}
	
	/**
	 * Get all players from instance world inside specified radius.
	 * @param object location of target
	 * @param radius radius around target
	 * @return players within radius
	 */
	public List<Player> getPlayersInsideRadius(ILocational @object, int radius)
	{
		List<Player> result = new LinkedList<>();
		foreach (Player player in _players)
		{
			if (player.isInsideRadius3D(@object, radius))
			{
				result.Add(player);
			}
		}
		return result;
	}
	
	/**
	 * Spawn doors inside instance world.
	 */
	private void spawnDoors()
	{
		foreach (DoorTemplate template in _template.getDoors().values())
		{
			// Create new door instance
			_doors.put(template.getId(), DoorData.getInstance().spawnDoor(template, this));
		}
	}
	
	/**
	 * Get all doors spawned inside instance world.
	 * @return collection of spawned doors
	 */
	public ICollection<Door> getDoors()
	{
		return _doors.values();
	}
	
	/**
	 * Get spawned door by template ID.
	 * @param id template ID of door
	 * @return instance of door if found, otherwise {@code null}
	 */
	public Door getDoor(int id)
	{
		return _doors.get(id);
	}
	
	/**
	 * Handle open/close status of instance doors.
	 * @param id ID of doors
	 * @param open {@code true} means open door, {@code false} means close door
	 */
	public void openCloseDoor(int id, bool open)
	{
		Door door = _doors.get(id);
		if (door != null)
		{
			if (open)
			{
				if (!door.isOpen())
				{
					door.openMe();
				}
			}
			else if (door.isOpen())
			{
				door.closeMe();
			}
		}
	}
	
	/**
	 * Check if spawn group with name {@code name} exists.
	 * @param name name of group to be checked
	 * @return {@code true} if group exist, otherwise {@code false}
	 */
	public bool isSpawnGroupExist(String name)
	{
		foreach (SpawnTemplate spawnTemplate in _spawns)
		{
			foreach (SpawnGroup group in spawnTemplate.getGroups())
			{
				if (name.equalsIgnoreCase(group.getName()))
				{
					return true;
				}
			}
		}
		return false;
	}
	
	/**
	 * Get spawn group by group name.
	 * @param name name of group
	 * @return list which contains spawn data from spawn group
	 */
	public List<SpawnGroup> getSpawnGroup(String name)
	{
		List<SpawnGroup> spawns = new();
		foreach (SpawnTemplate spawnTemplate in _spawns)
		{
			spawns.AddRange(spawnTemplate.getGroupsByName(name));
		}
		return spawns;
	}
	
	/**
	 * @param name
	 * @return {@code List} of NPCs that are part of specified group
	 */
	public List<Npc> getNpcsOfGroup(String name)
	{
		return getNpcsOfGroup(name, null);
	}
	
	/**
	 * @param groupName
	 * @param filterValue
	 * @return {@code List} of NPCs that are part of specified group and matches filter specified
	 */
	public List<Npc> getNpcsOfGroup(String groupName, Predicate<Npc> filterValue)
	{
		Predicate<Npc> filter = filterValue;
		if (filter == null)
		{
			filter = x => x is not null;
		}
		
		List<Npc> npcs = new();
		foreach (SpawnTemplate spawnTemplate in _spawns)
		{
			foreach (SpawnGroup group in spawnTemplate.getGroupsByName(groupName))
			{
				foreach (NpcSpawnTemplate npcTemplate in group.getSpawns())
				{
					foreach (Npc npc in npcTemplate.getSpawnedNpcs())
					{
						if (filter(npc))
						{
							npcs.Add(npc);
						}
					}
				}
			}
		}
		return npcs;
	}
	
	/**
	 * @param groupName
	 * @param filterValue
	 * @return {@code Npc} instance of an NPC that is part of a group and matches filter specified
	 */
	public Npc getNpcOfGroup(String groupName, Predicate<Npc> filterValue)
	{
		Predicate<Npc> filter = filterValue;
		if (filter == null)
		{
			filter = x => x is not null;
		}
		
		foreach (SpawnTemplate spawnTemplate in _spawns)
		{
			foreach (SpawnGroup group in spawnTemplate.getGroupsByName(groupName))
			{
				foreach (NpcSpawnTemplate npcTemplate in group.getSpawns())
				{
					foreach (Npc npc in npcTemplate.getSpawnedNpcs())
					{
						if (filter(npc))
						{
							return npc;
						}
					}
				}
			}
		}
		return null;
	}
	
	/**
	 * Spawn NPCs from group (defined in XML template) into instance world.
	 * @param name name of group which should be spawned
	 * @return list that contains NPCs spawned by this method
	 */
	public List<Npc> spawnGroup(String name)
	{
		List<SpawnGroup> spawns = getSpawnGroup(name);
		if (spawns == null)
		{
			LOGGER.Warn("Spawn group " + name + " doesn't exist for instance " + _template.getName() + " (" + _id + ")!");
			return Collections.emptyList();
		}
		
		List<Npc> npcs = new();
		try
		{
			foreach (SpawnGroup holder in spawns)
			{
				holder.spawnAll(this);
				holder.getSpawns().forEach(spawn -> npcs.addAll(spawn.getSpawnedNpcs()));
			}
		}
		catch (Exception e)
		{
			LOGGER.Warn("Unable to spawn group " + name + " inside instance " + _template.getName() + " (" + _id + ")");
		}
		return npcs;
	}
	
	/**
	 * De-spawns NPCs from group (defined in XML template) from the instance world.
	 * @param name of group which should be de-spawned
	 */
	public void despawnGroup(String name)
	{
		List<SpawnGroup> spawns = getSpawnGroup(name);
		if (spawns == null)
		{
			LOGGER.Warn("Spawn group " + name + " doesn't exist for instance " + _template.getName() + " (" + _id + ")!");
			return;
		}
		
		try
		{
			spawns.ForEach(x => x.despawnAll());
		}
		catch (Exception e)
		{
			LOGGER.Warn("Unable to spawn group " + name + " inside instance " + _template.getName() + " (" + _id + ")");
		}
	}
	
	/**
	 * Get spawned NPCs from instance.
	 * @return set of NPCs from instance
	 */
	public Set<Npc> getNpcs()
	{
		return _npcs;
	}
	
	/**
	 * Get spawned NPCs from instance with specific IDs.
	 * @param id IDs of NPCs which should be found
	 * @return list of filtered NPCs from instance
	 */
	public List<Npc> getNpcs(params int[] id)
	{
		List<Npc> result = new();
		foreach (Npc npc in _npcs.Keys)
		{
			if (CommonUtil.contains(id, npc.getId()))
			{
				result.Add(npc);
			}
		}
		return result;
	}
	
	/**
	 * Get spawned NPCs from instance with specific IDs and class type.
	 * @param <T>
	 * @param clazz
	 * @param ids IDs of NPCs which should be found
	 * @return list of filtered NPCs from instance
	 */
	public List<T> getNpcs<T>(params int[] ids)
		where T: Npc
	{
		List<T> result = new();
		foreach (Npc npc in _npcs.Keys)
		{
			if (((ids.Length == 0) || CommonUtil.contains(ids, npc.getId())) && (npc is T))
			{
				result.Add((T) npc);
			}
		}
		return result;
	}
	
	/**
	 * Get alive NPCs from instance.
	 * @return set of NPCs from instance
	 */
	public List<Npc> getAliveNpcs()
	{
		List<Npc> result = new();
		foreach (Npc npc in _npcs.Keys)
		{
			if (npc.getCurrentHp() > 0)
			{
				result.Add(npc);
			}
		}
		return result;
	}
	
	/**
	 * Get alive NPCs from instance with specific IDs.
	 * @param id IDs of NPCs which should be found
	 * @return list of filtered NPCs from instance
	 */
	public List<Npc> getAliveNpcs(params int[] id)
	{
		List<Npc> result = new();
		foreach (Npc npc in _npcs.Keys)
		{
			if ((npc.getCurrentHp() > 0) && CommonUtil.contains(id, npc.getId()))
			{
				result.Add(npc);
			}
		}
		return result;
	}
	
	/**
	 * Get spawned and alive NPCs from instance with specific IDs and class type.
	 * @param <T>
	 * @param clazz
	 * @param ids IDs of NPCs which should be found
	 * @return list of filtered NPCs from instance
	 */
	public List<T> getAliveNpcs<T>(params int[] ids)
		where T: Npc
	{
		List<T> result = new();
		foreach (Npc npc in _npcs.Keys)
		{
			if ((((ids.Length == 0) || CommonUtil.contains(ids, npc.getId())) && (npc.getCurrentHp() > 0)) && (npc is T))
			{
				result.Add((T) npc);
			}
		}
		return result;
	}
	
	/**
	 * Get alive NPC count from instance.
	 * @return count of filtered NPCs from instance
	 */
	public int getAliveNpcCount()
	{
		int count = 0;
		foreach (Npc npc in _npcs.Keys)
		{
			if (npc.getCurrentHp() > 0)
			{
				count++;
			}
		}
		return count;
	}
	
	/**
	 * Get alive NPC count from instance with specific IDs.
	 * @param id IDs of NPCs which should be counted
	 * @return count of filtered NPCs from instance
	 */
	public int getAliveNpcCount(params int[] id)
	{
		int count = 0;
		foreach (Npc npc in _npcs.Keys)
		{
			if ((npc.getCurrentHp() > 0) && CommonUtil.contains(id, npc.getId()))
			{
				count++;
			}
		}
		return count;
	}
	
	/**
	 * Get first found spawned NPC with specific ID.
	 * @param id ID of NPC to be found
	 * @return first found NPC with specified ID, otherwise {@code null}
	 */
	public Npc getNpc(int id)
	{
		foreach (Npc npc in _npcs.Keys)
		{
			if (npc.getId() == id)
			{
				return npc;
			}
		}
		return null;
	}
	
	public void addNpc(Npc npc)
	{
		_npcs.add(npc);
	}
	
	public void removeNpc(Npc npc)
	{
		_npcs.remove(npc);
	}
	
	/**
	 * Remove all players from instance world.
	 */
	private void removePlayers()
	{
		_players.ForEach(this::ejectPlayer);
		_players.clear();
	}
	
	/**
	 * Despawn doors inside instance world.
	 */
	private void removeDoors()
	{
		foreach (Door door in _doors.values())
		{
			if (door != null)
			{
				door.decayMe();
			}
		}
		_doors.clear();
	}
	
	/**
	 * Despawn NPCs inside instance world.
	 */
	public void removeNpcs()
	{
		_spawns.ForEach(x => x.despawnAll());
		_npcs.ForEach(Npc::deleteMe);
		_npcs.clear();
	}
	
	/**
	 * Change instance duration.
	 * @param minutes remaining time to destroy instance
	 */
	public void setDuration(int minutes)
	{
		// Instance never ends
		if (minutes < 0)
		{
			_endTime = -1;
			return;
		}
		
		// Stop running tasks
		long millis = TimeUnit.MINUTES.toMillis(minutes);
		if (_cleanUpTask != null)
		{
			_cleanUpTask.cancel(true);
			_cleanUpTask = null;
		}
		
		if ((_emptyDestroyTask != null) && (millis < _emptyDestroyTask.getDelay(TimeUnit.MILLISECONDS)))
		{
			_emptyDestroyTask.cancel(true);
			_emptyDestroyTask = null;
		}
		
		// Set new cleanup task
		_endTime = System.currentTimeMillis() + millis;
		if (minutes < 1) // Destroy instance
		{
			destroy();
		}
		else
		{
			sendWorldDestroyMessage(minutes);
			if (minutes <= 5) // Message 1 minute before destroy
			{
				_cleanUpTask = ThreadPool.schedule(this::cleanUp, millis - 60000);
			}
			else // Message 5 minutes before destroy
			{
				_cleanUpTask = ThreadPool.schedule(this::cleanUp, millis - (5 * 60000));
			}
		}
	}
	
	/**
	 * Destroy current instance world.<br>
	 * <b><font color=red>Use this method to destroy instance world properly.</font></b>
	 */
	public synchronized void destroy()
	{
		if (_cleanUpTask != null)
		{
			_cleanUpTask.cancel(false);
			_cleanUpTask = null;
		}
		
		if (_emptyDestroyTask != null)
		{
			_emptyDestroyTask.cancel(false);
			_emptyDestroyTask = null;
		}
		
		_ejectDeadTasks.values().forEach(t => t.cancel(true));
		_ejectDeadTasks.clear();
		
		// Notify DP scripts
		if (!isDynamic() && EventDispatcher.getInstance().hasListener(EventType.ON_INSTANCE_DESTROY, _template))
		{
			EventDispatcher.getInstance().notifyEvent(new OnInstanceDestroy(this), _template);
		}
		
		removePlayers();
		removeDoors();
		removeNpcs();
		
		InstanceManager.getInstance().unregister(getId());
	}
	
	/**
	 * Teleport player out of instance.
	 * @param player player that should be moved out
	 */
	public void ejectPlayer(Player player)
	{
		Instance world = player.getInstanceWorld();
		if ((world != null) && world.equals(this))
		{
			Location loc = _template.getExitLocation(player);
			if (loc != null)
			{
				player.teleToLocation(loc, null);
			}
			else
			{
				player.teleToLocation(TeleportWhereType.TOWN, null);
			}
		}
	}
	
	/**
	 * Send packet to each player from instance world.
	 * @param packets packets to be send
	 */
	public void broadcastPacket(params ServerPacket[] packets)
	{
		foreach (Player player in _players.Keys)
		{
			foreach (ServerPacket packet in packets)
			{
				player.sendPacket(packet);
			}
		}
	}
	
	/**
	 * Get instance creation time.
	 * @return creation time in milliseconds
	 */
	public long getStartTime()
	{
		return _startTime;
	}
	
	/**
	 * Get elapsed time since instance create.
	 * @return elapsed time in milliseconds
	 */
	public long getElapsedTime()
	{
		return System.currentTimeMillis() - _startTime;
	}
	
	/**
	 * Get remaining time before instance will be destroyed.
	 * @return remaining time in milliseconds if duration is not equal to -1, otherwise -1
	 */
	public long getRemainingTime()
	{
		return (_endTime == -1) ? -1 : (_endTime - System.currentTimeMillis());
	}
	
	/**
	 * Get instance destroy time.
	 * @return destroy time in milliseconds if duration is not equal to -1, otherwise -1
	 */
	public long getEndTime()
	{
		return _endTime;
	}
	
	/**
	 * Set reenter penalty for players associated with current instance.<br>
	 * Penalty time is calculated from XML reenter data.
	 */
	public void setReenterTime()
	{
		setReenterTime(_template.calculateReenterTime());
	}
	
	/**
	 * Set reenter penalty for players associated with current instance.
	 * @param time penalty time in milliseconds since January 1, 1970
	 */
	public void setReenterTime(long time)
	{
		// Cannot store reenter data for instance without template id.
		if ((_template.getId() == -1) && (time > 0))
		{
			return;
		}
		
		try 
		{
			using GameServerDbContext ctx = new();
			PreparedStatement ps =
				con.prepareStatement(
					"INSERT IGNORE INTO character_instance_time (charId,instanceId,time) VALUES (?,?,?)");
			
			// Save to database
			foreach (int playerId in _allowed)
			{
				ps.setInt(1, playerId);
				ps.setInt(2, _template.getId());
				ps.setLong(3, time);
				ps.addBatch();
			}
			ps.executeBatch();
			
			// Save to memory and send message to player
			SystemMessage msg = new SystemMessage(SystemMessageId.INSTANCE_ZONE_S1_S_ENTRY_HAS_BEEN_RESTRICTED_YOU_CAN_CHECK_THE_NEXT_POSSIBLE_ENTRY_TIME_WITH_INSTANCEZONE);
			if (InstanceManager.getInstance().getInstanceName(getTemplateId()) != null)
			{
				msg.addInstanceName(_template.getId());
			}
			else
			{
				msg.addString(_template.getName());
			}
			_allowed.ForEach(playerId =>
			{
				InstanceManager.getInstance().setReenterPenalty(playerId, getTemplateId(), time);
				Player player = World.getInstance().getPlayer(playerId);
				if ((player != null) && player.isOnline())
				{
					player.sendPacket(msg);
				}
			});
		}
		catch (Exception e)
		{
			LOGGER.Warn("Could not insert character instance reenter data: " + e);
		}
	}
	
	/**
	 * Set instance world to finish state.<br>
	 * Calls method {@link Instance#finishInstance(int)} with {@link Config#INSTANCE_FINISH_TIME} as argument.
	 */
	public void finishInstance()
	{
		finishInstance(Config.INSTANCE_FINISH_TIME);
	}
	
	/**
	 * Set instance world to finish state.<br>
	 * Set re-enter for allowed players if required data are defined in template.<br>
	 * Change duration of instance and set empty destroy time to 0 (instant effect).
	 * @param delay delay in minutes
	 */
	public void finishInstance(int delay)
	{
		// Set re-enter for players
		if (_template.getReenterType() == InstanceReenterType.ON_FINISH)
		{
			setReenterTime();
		}
		// Change instance duration
		setDuration(delay);
	}
	
	// ---------------------------------------------
	// Listeners
	// ---------------------------------------------
	/**
	 * This method is called when player dies inside instance.
	 * @param player
	 */
	public void onDeath(Player player)
	{
		if (!player.isOnEvent() && (_template.getEjectTime() > 0))
		{
			// Send message
			SystemMessage sm = new SystemMessage(SystemMessageId.IF_YOU_ARE_NOT_RESURRECTED_IN_S1_MIN_YOU_WILL_BE_TELEPORTED_OUT_OF_THE_INSTANCE_ZONE);
			sm.addInt(_template.getEjectTime());
			player.sendPacket(sm);
			
			// Start eject task
			_ejectDeadTasks.put(player.getObjectId(), ThreadPool.schedule(() =>
			{
				if (player.isDead())
				{
					ejectPlayer(player.getActingPlayer());
				}
			}, _template.getEjectTime() * 60 * 1000)); // minutes to milliseconds
		}
	}
	
	/**
	 * This method is called when player was resurrected inside instance.
	 * @param player resurrected player
	 */
	public void doRevive(Player player)
	{
		ScheduledFuture<?> task = _ejectDeadTasks.remove(player.getObjectId());
		if (task != null)
		{
			task.cancel(true);
		}
	}
	
	/**
	 * This method is called when object enter or leave this instance.
	 * @param object instance of object which enters/leaves instance
	 * @param enter {@code true} when object enter, {@code false} when object leave
	 */
	public void onInstanceChange(WorldObject @object, bool enter)
	{
		if (@object.isPlayer())
		{
			Player player = @object.getActingPlayer();
			if (enter)
			{
				addPlayer(player);
				
				// Set origin return location if enabled
				if (_template.getExitLocationType() == InstanceTeleportType.ORIGIN)
				{
					player.getVariables().set(PlayerVariables.INSTANCE_ORIGIN, player.getX() + ";" + player.getY() + ";" + player.getZ());
				}
				
				// Remove player buffs
				if (_template.isRemoveBuffEnabled())
				{
					_template.removePlayerBuff(player);
				}
				
				// Notify DP scripts
				if (!isDynamic() && EventDispatcher.getInstance().hasListener(EventType.ON_INSTANCE_ENTER, _template))
				{
					EventDispatcher.getInstance().notifyEventAsync(new OnInstanceEnter(player, this), _template);
				}
			}
			else
			{
				removePlayer(player);
				
				// Notify DP scripts
				if (!isDynamic() && EventDispatcher.getInstance().hasListener(EventType.ON_INSTANCE_LEAVE, _template))
				{
					EventDispatcher.getInstance().notifyEventAsync(new OnInstanceLeave(player, this), _template);
				}
			}
		}
		else if (@object.isNpc())
		{
			Npc npc = (Npc) @object;
			if (enter)
			{
				addNpc(npc);
			}
			else
			{
				if (npc.getSpawn() != null)
				{
					npc.getSpawn().stopRespawn();
				}
				removeNpc(npc);
			}
		}
	}
	
	/**
	 * This method is called when player logout inside instance world.
	 * @param player player who logout
	 */
	public void onPlayerLogout(Player player)
	{
		removePlayer(player);
		if (Config.RESTORE_PLAYER_INSTANCE)
		{
			player.getVariables().set("INSTANCE_RESTORE", _id);
		}
		else
		{
			Location loc = getExitLocation(player);
			if (loc != null)
			{
				player.setLocationInvisible(loc);
				// If player has death pet, put him out of instance world
				Summon pet = player.getPet();
				if (pet != null)
				{
					pet.teleToLocation(loc, true);
				}
			}
		}
	}
	
	// ----------------------------------------------
	// Template methods
	// ----------------------------------------------
	/**
	 * Get parameters from instance template.
	 * @return template parameters
	 */
	public StatSet getTemplateParameters()
	{
		return _template.getParameters();
	}
	
	/**
	 * Get template ID of instance world.
	 * @return instance template ID
	 */
	public int getTemplateId()
	{
		return _template.getId();
	}
	
	/**
	 * Get type of re-enter data.
	 * @return type of re-enter (see {@link InstanceReenterType} for possible values)
	 */
	public InstanceReenterType getReenterType()
	{
		return _template.getReenterType();
	}
	
	/**
	 * Check if instance world is PvP zone.
	 * @return {@code true} when instance is PvP zone, otherwise {@code false}
	 */
	public bool isPvP()
	{
		return _template.isPvP();
	}
	
	/**
	 * Check if summoning players to instance world is allowed.
	 * @return {@code true} when summon is allowed, otherwise {@code false}
	 */
	public bool isPlayerSummonAllowed()
	{
		return _template.isPlayerSummonAllowed();
	}
	
	/**
	 * Get enter location for instance world.
	 * @return {@link Location} object if instance has enter location defined, otherwise {@code null}
	 */
	public Location getEnterLocation()
	{
		return _template.getEnterLocation();
	}
	
	/**
	 * Get all enter locations defined in XML template.
	 * @return list of enter locations
	 */
	public List<Location> getEnterLocations()
	{
		return _template.getEnterLocations();
	}
	
	/**
	 * Get exit location for player from instance world.
	 * @param player instance of player who wants to leave instance world
	 * @return {@link Location} object if instance has exit location defined, otherwise {@code null}
	 */
	public Location getExitLocation(Player player)
	{
		return _template.getExitLocation(player);
	}
	
	/**
	 * @return the exp rate of the instance
	 */
	public float getExpRate()
	{
		return _template.getExpRate();
	}
	
	/**
	 * @return the sp rate of the instance
	 */
	public float getSPRate()
	{
		return _template.getSPRate();
	}
	
	/**
	 * @return the party exp rate of the instance
	 */
	public float getExpPartyRate()
	{
		return _template.getExpPartyRate();
	}
	
	/**
	 * @return the party sp rate of the instance
	 */
	public float getSPPartyRate()
	{
		return _template.getSPPartyRate();
	}
	
	// ----------------------------------------------
	// Tasks
	// ----------------------------------------------
	/**
	 * Clean up instance.
	 */
	private void cleanUp()
	{
		if (getRemainingTime() <= TimeUnit.MINUTES.toMillis(1))
		{
			sendWorldDestroyMessage(1);
			_cleanUpTask = ThreadPool.schedule(this::destroy, 60 * 1000); // 1 minute
		}
		else
		{
			sendWorldDestroyMessage(5);
			_cleanUpTask = ThreadPool.schedule(this::cleanUp, 5 * 60 * 1000); // 5 minutes
		}
	}
	
	/**
	 * Show instance destroy messages to players inside instance world.
	 * @param delay time in minutes
	 */
	private void sendWorldDestroyMessage(int delay)
	{
		// Dimensional wrap does not show timer after 5 minutes.
		if (delay > 5)
		{
			return;
		}
		SystemMessage sm = new SystemMessage(SystemMessageId.THE_INSTANCE_ZONE_EXPIRES_IN_S1_MIN_AFTER_THAT_YOU_WILL_BE_TELEPORTED_OUTSIDE_2);
		sm.addInt(delay);
		broadcastPacket(sm);
	}
	
	public override bool Equals(Object? obj)
	{
		return (obj is Instance) && (((Instance) obj).getId() == getId());
	}
	
	public override String ToString()
	{
		return _template.getName() + "(" + _id + ")";
	}
}