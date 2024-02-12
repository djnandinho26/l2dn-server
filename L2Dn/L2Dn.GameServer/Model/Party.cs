﻿using System.Runtime.CompilerServices;
using L2Dn.GameServer.Enums;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Utilities;
using NLog;
using ThreadPool = System.Threading.ThreadPool;

namespace L2Dn.GameServer.Model;

/**
 * This class serves as a container for player parties.
 * @author nuocnam
 */
public class Party : AbstractPlayerGroup
{
	private static readonly Logger LOGGER = LogManager.GetLogger(nameof(Party));
	
	private static readonly double[] BONUS_EXP_SP =
	{
		1.0, 1.6, 1.65, 1.7, 1.8, 1.9, 2.0, 2.1, 2.2
	};
	
	private static readonly Duration PARTY_POSITION_BROADCAST_INTERVAL = Duration.ofSeconds(12);
	private static readonly Duration PARTY_DISTRIBUTION_TYPE_REQUEST_TIMEOUT = Duration.ofSeconds(15);
	
	private readonly List<Player> _members = new(); // TODO: CopyOnWriteArrayList
	private bool _pendingInvitation = false;
	private long _pendingInviteTimeout;
	private int _partyLvl = 0;
	private PartyDistributionType _distributionType = PartyDistributionType.FINDERS_KEEPERS;
	private PartyDistributionType _changeRequestDistributionType;
	private Future<?> _changeDistributionTypeRequestTask = null;
	private Set<int> _changeDistributionTypeAnswers = null;
	private int _itemLastLoot = 0;
	private CommandChannel _commandChannel = null;
	private Future<?> _positionBroadcastTask = null;
	protected PartyMemberPosition _positionPacket;
	private bool _disbanding = false;
	private Map<int, Creature> _tacticalSigns = null;
	private static readonly int[] TACTICAL_SYS_STRINGS =
	{
		0,
		2664,
		2665,
		2666,
		2667
	};
	
	/**
	 * Construct a new Party object with a single member - the leader.
	 * @param leader the leader of this party
	 * @param partyDistributionType the item distribution rule of this party
	 */
	public Party(Player leader, PartyDistributionType partyDistributionType)
	{
		_members.Add(leader);
		_partyLvl = leader.getLevel();
		_distributionType = partyDistributionType;
		World.getInstance().incrementParty();
	}
	
	/**
	 * Check if another player can start invitation process.
	 * @return {@code true} if this party waits for a response on an invitation, {@code false} otherwise
	 */
	public bool getPendingInvitation()
	{
		return _pendingInvitation;
	}
	
	/**
	 * Set invitation process flag and store time for expiration.<br>
	 * Happens when a player joins party or declines to join.
	 * @param value the pending invitation state to set
	 */
	public void setPendingInvitation(bool value)
	{
		_pendingInvitation = value;
		_pendingInviteTimeout = GameTimeTaskManager.getInstance().getGameTicks() + (Player.REQUEST_TIMEOUT * GameTimeTaskManager.TICKS_PER_SECOND);
	}
	
	/**
	 * Check if a player invitation request is expired.
	 * @return {@code true} if time is expired, {@code false} otherwise
	 * @see org.l2jmobius.gameserver.model.actor.Player#isRequestExpired()
	 */
	public bool isInvitationRequestExpired()
	{
		return (_pendingInviteTimeout <= GameTimeTaskManager.getInstance().getGameTicks());
	}
	
	/**
	 * Get a random member from this party.
	 * @param itemId the ID of the item for which the member must have inventory space
	 * @param target the object of which the member must be within a certain range (must not be null)
	 * @return a random member from this party or {@code null} if none of the members have inventory space for the specified item
	 */
	private Player getCheckedRandomMember(int itemId, Creature target)
	{
		List<Player> availableMembers = new();
		foreach (Player member in _members)
		{
			if (member.getInventory().validateCapacityByItemId(itemId) && Util.checkIfInRange(Config.ALT_PARTY_RANGE, target, member, true))
			{
				availableMembers.Add(member);
			}
		}
		return !availableMembers.isEmpty() ? availableMembers.get(Rnd.get(availableMembers.size())) : null;
	}
	
	/**
	 * get next item looter
	 * @param itemId
	 * @param target
	 * @return
	 */
	private Player getCheckedNextLooter(int itemId, Creature target)
	{
		for (int i = 0; i < getMemberCount(); i++)
		{
			if (++_itemLastLoot >= getMemberCount())
			{
				_itemLastLoot = 0;
			}
			try
			{
				Player member = _members[_itemLastLoot];
				if (member.getInventory().validateCapacityByItemId(itemId) && Util.checkIfInRange(Config.ALT_PARTY_RANGE, target, member, true))
				{
					return member;
				}
			}
			catch (Exception e)
			{
				// continue, take another member if this just logged off
			}
		}
		return null;
	}
	
	/**
	 * get next item looter
	 * @param player
	 * @param itemId
	 * @param spoil
	 * @param target
	 * @return
	 */
	private Player getActualLooter(Player player, int itemId, bool spoil, Creature target)
	{
		Player looter = null;
		
		switch (_distributionType)
		{
			case PartyDistributionType.RANDOM:
			{
				if (!spoil)
				{
					looter = getCheckedRandomMember(itemId, target);
				}
				break;
			}
			case PartyDistributionType.RANDOM_INCLUDING_SPOIL:
			{
				looter = getCheckedRandomMember(itemId, target);
				break;
			}
			case PartyDistributionType.BY_TURN:
			{
				if (!spoil)
				{
					looter = getCheckedNextLooter(itemId, target);
				}
				break;
			}
			case PartyDistributionType.BY_TURN_INCLUDING_SPOIL:
			{
				looter = getCheckedNextLooter(itemId, target);
				break;
			}
		}
		return looter != null ? looter : player;
	}
	
	/**
	 * Broadcasts UI update and User Info for new party leader.
	 */
	public void broadcastToPartyMembersNewLeader()
	{
		foreach (Player member in _members)
		{
			if (member != null)
			{
				member.sendPacket(PartySmallWindowDeleteAll.STATIC_PACKET);
				member.sendPacket(new PartySmallWindowAll(member, this));
				member.broadcastUserInfo();
			}
		}
	}
	
	/**
	 * Send a Server->Client packet to all other Player of the Party.
	 * @param player
	 * @param packet
	 */
	public void broadcastToPartyMembers(Player player, ServerPacket packet)
	{
		foreach (Player member in _members)
		{
			if ((member != null) && (member.getObjectId() != player.getObjectId()))
			{
				member.sendPacket(packet);
			}
		}
	}
	
	/**
	 * adds new member to party
	 * @param player
	 */
	public void addPartyMember(Player player)
	{
		if (_members.Contains(player))
		{
			return;
		}
		
		if (_changeRequestDistributionType != null)
		{
			finishLootRequest(false); // cancel on invite
		}
		
		// add player to party
		_members.Add(player);
		
		// sends new member party window for all members
		// we do all actions before adding member to a list, this speeds things up a little
		player.sendPacket(new PartySmallWindowAll(player, this));
		
		// sends pets/summons of party members
		foreach (Player pMember in _members)
		{
			if (pMember != null)
			{
				Summon pet = pMember.getPet();
				if (pet != null)
				{
					player.sendPacket(new ExPartyPetWindowAdd(pet));
				}
				pMember.getServitors().values().forEach(s => player.sendPacket(new ExPartyPetWindowAdd(s)));
			}
		}
		
		SystemMessage msg = new SystemMessage(SystemMessageId.YOU_HAVE_JOINED_A_PARTY);
		player.sendPacket(msg);
		
		msg = new SystemMessage(SystemMessageId.C1_HAS_JOINED_THE_PARTY);
		msg.addString(player.getName());
		broadcastPacket(msg);
		
		foreach (Player member in _members)
		{
			if (member != player)
			{
				member.sendPacket(new PartySmallWindowAdd(player, this));
			}
		}
		
		// send the position of all party members to the new party member
		// player.sendPacket(new PartyMemberPosition(this));
		// send the position of the new party member to all party members (except the new one - he knows his own position)
		// broadcastToPartyMembers(player, new PartyMemberPosition(this));
		
		// if member has pet/summon add it to other as well
		Summon pet = player.getPet();
		if (pet != null)
		{
			broadcastPacket(new ExPartyPetWindowAdd(pet));
		}
		
		player.getServitors().values().forEach(s -> broadcastPacket(new ExPartyPetWindowAdd(s)));
		
		// adjust party level
		if (player.getLevel() > _partyLvl)
		{
			_partyLvl = player.getLevel();
		}
		
		// status update for hp bar display
		StatusUpdate su = new StatusUpdate(player);
		su.addUpdate(StatusUpdateType.MAX_HP, player.getMaxHp());
		su.addUpdate(StatusUpdateType.CUR_HP, (int) player.getCurrentHp());
		
		// update partySpelled
		Summon summon;
		foreach (Player member in _members)
		{
			if (member != null)
			{
				member.updateEffectIcons(true); // update party icons only
				summon = member.getPet();
				member.broadcastUserInfo();
				if (summon != null)
				{
					summon.updateEffectIcons();
				}
				member.getServitors().values().forEach(Summon.updateEffectIcons);
				
				// send hp status update
				member.sendPacket(su);
			}
		}
		
		// open the CCInformationwindow
		if (isInCommandChannel())
		{
			player.sendPacket(ExOpenMPCC.STATIC_PACKET);
		}
		
		if (_positionBroadcastTask == null)
		{
			_positionBroadcastTask = ThreadPool.scheduleAtFixedRate(() =>
			{
				if (_positionPacket == null)
				{
					_positionPacket = new PartyMemberPosition(this);
				}
				else
				{
					_positionPacket.reuse(this);
				}
				broadcastPacket(_positionPacket);
			}, PARTY_POSITION_BROADCAST_INTERVAL.toMillis() / 2, PARTY_POSITION_BROADCAST_INTERVAL.toMillis());
		}
		applyTacticalSigns(player, false);
		World.getInstance().incrementPartyMember();
	}
	
	private Map<int, Creature> getTacticalSigns()
	{
		if (_tacticalSigns == null)
		{
			lock (this)
			{
				if (_tacticalSigns == null)
				{
					_tacticalSigns = new ConcurrentHashMap<>(1);
				}
			}
		}
		return _tacticalSigns;
	}
	
	public void applyTacticalSigns(Player player, bool remove)
	{
		if (_tacticalSigns == null)
		{
			return;
		}
		
		_tacticalSigns.entrySet().forEach(entry => player.sendPacket(new ExTacticalSign(entry.getValue(), remove ? 0 : entry.getKey())));
	}
	
	public void addTacticalSign(Player player, int tacticalSignId, Creature target)
	{
		Creature tacticalTarget = getTacticalSigns().get(tacticalSignId);
		if (tacticalTarget == null)
		{
			// if the new sign is applied to an existing target, remove the old sign from map
			_tacticalSigns.values().remove(target);
			
			// Add the new sign
			_tacticalSigns.put(tacticalSignId, target);
			
			final SystemMessage sm = new SystemMessage(SystemMessageId.C1_USED_S3_ON_C2);
			sm.addPcName(player);
			sm.addString(target.getName());
			sm.addSystemString(TACTICAL_SYS_STRINGS[tacticalSignId]);
			
			_members.forEach(m =>
			{
				m.sendPacket(new ExTacticalSign(target, tacticalSignId));
				m.sendPacket(sm);
			});
		}
		else if (tacticalTarget == target)
		{
			// Sign already assigned
			// If the sign is applied on the same target, remove it
			_tacticalSigns.remove(tacticalSignId);
			_members.forEach(m => m.sendPacket(new ExTacticalSign(tacticalTarget, 0)));
		}
		else
		{
			// Otherwise, delete the old sign, and apply it to the new target
			_tacticalSigns.replace(tacticalSignId, target);
			
			SystemMessage sm = new SystemMessage(SystemMessageId.C1_USED_S3_ON_C2);
			sm.addPcName(player);
			sm.addString(target.getName());
			sm.addSystemString(TACTICAL_SYS_STRINGS[tacticalSignId]);
			
			_members.ForEach(m =>
			{
				m.sendPacket(new ExTacticalSign(tacticalTarget, 0));
				m.sendPacket(new ExTacticalSign(target, tacticalSignId));
				m.sendPacket(sm);
			});
		}
	}
	
	public void setTargetBasedOnTacticalSignId(Player player, int tacticalSignId)
	{
		if (_tacticalSigns == null)
		{
			return;
		}
		
		Creature tacticalTarget = _tacticalSigns.get(tacticalSignId);
		if ((tacticalTarget != null) && !tacticalTarget.isInvisible() && tacticalTarget.isTargetable() && !player.isTargetingDisabled())
		{
			player.setTarget(tacticalTarget);
		}
	}
	
	/**
	 * Removes a party member using its name.
	 * @param name player the player to be removed from the party.
	 * @param type the message type {@link PartyMessageType}.
	 */
	public void removePartyMember(String name, PartyMessageType type)
	{
		removePartyMember(getPlayerByName(name), type);
	}
	
	/**
	 * Removes a party member instance.
	 * @param player the player to be removed from the party.
	 * @param type the message type {@link PartyMessageType}.
	 */
	public void removePartyMember(Player player, PartyMessageType type)
	{
		if (_members.Contains(player))
		{
			bool isLeader = isLeader(player);
			if (!_disbanding && ((_members.Count == 2) || (isLeader && !Config.ALT_LEAVE_PARTY_LEADER && (type != PartyMessageType.DISCONNECTED))))
			{
				disbandParty();
				return;
			}
			
			_members.Remove(player);
			recalculatePartyLevel();
			
			if (player.isInDuel())
			{
				DuelManager.getInstance().onRemoveFromParty(player);
			}
			
			try
			{
				// Channeling a player!
				if (player.isChanneling() && (player.getSkillChannelizer().hasChannelized()))
				{
					player.abortCast();
				}
				else if (player.isChannelized())
				{
					player.getSkillChannelized().abortChannelization();
				}
			}
			catch (Exception e)
			{
				LOGGER.Warn(e);
			}
			
			SystemMessage msg;
			if (type == PartyMessageType.EXPELLED)
			{
				player.sendPacket(SystemMessageId.YOU_ARE_DISMISSED_FROM_THE_PARTY);
				msg = new SystemMessage(SystemMessageId.C1_IS_DISMISSED_FROM_THE_PARTY);
				msg.addString(player.getName());
				broadcastPacket(msg);
			}
			else if ((type == PartyMessageType.LEFT) || (type == PartyMessageType.DISCONNECTED))
			{
				player.sendPacket(SystemMessageId.YOU_HAVE_LEFT_THE_PARTY);
				msg = new SystemMessage(SystemMessageId.C1_HAS_LEFT_THE_PARTY);
				msg.addString(player.getName());
				broadcastPacket(msg);
			}
			
			World.getInstance().decrementPartyMember();
			
			// UI update.
			player.sendPacket(PartySmallWindowDeleteAll.STATIC_PACKET);
			player.setParty(null);
			broadcastPacket(new PartySmallWindowDelete(player));
			Summon pet = player.getPet();
			if (pet != null)
			{
				broadcastPacket(new ExPartyPetWindowDelete(pet));
			}
			player.getServitors().values().forEach(s => player.sendPacket(new ExPartyPetWindowDelete(s)));
			
			// Close the CCInfoWindow
			if (isInCommandChannel())
			{
				player.sendPacket(ExCloseMPCC.STATIC_PACKET);
			}
			if (isLeader && (_members.size() > 1) && (Config.ALT_LEAVE_PARTY_LEADER || (type == PartyMessageType.DISCONNECTED)))
			{
				msg = new SystemMessage(SystemMessageId.C1_HAS_BECOME_THE_PARTY_LEADER);
				msg.addString(getLeader().getName());
				broadcastPacket(msg);
				broadcastToPartyMembersNewLeader();
			}
			else if (_members.Count == 1)
			{
				if (isInCommandChannel())
				{
					// delete the whole command channel when the party who opened the channel is disbanded
					if (_commandChannel.getLeader().getObjectId() == getLeader().getObjectId())
					{
						_commandChannel.disbandChannel();
					}
					else
					{
						_commandChannel.removeParty(this);
					}
				}
				
				Player leader = getLeader();
				if (leader != null)
				{
					applyTacticalSigns(leader, true);
					
					leader.setParty(null);
					if (leader.isInDuel())
					{
						DuelManager.getInstance().onRemoveFromParty(leader);
					}
				}
				
				if (_changeDistributionTypeRequestTask != null)
				{
					_changeDistributionTypeRequestTask.cancel(true);
					_changeDistributionTypeRequestTask = null;
				}
				if (_positionBroadcastTask != null)
				{
					_positionBroadcastTask.cancel(false);
					_positionBroadcastTask = null;
				}
				_members.Clear();
			}
			applyTacticalSigns(player, true);
		}
	}
	
	/**
	 * Disperse a party and send a message to all its members.
	 */
	public void disbandParty()
	{
		_disbanding = true;
		broadcastPacket(new SystemMessage(SystemMessageId.THE_PARTY_IS_DISBANDED));
		foreach (Player member in _members)
		{
			if (member != null)
			{
				removePartyMember(member, PartyMessageType.NONE);
			}
		}
		World.getInstance().decrementParty();
	}
	
	/**
	 * Change party leader (used for string arguments)
	 * @param name the name of the player to set as the new party leader
	 */
	public void changePartyLeader(String name)
	{
		setLeader(getPlayerByName(name));
	}
	
	public override void setLeader(Player player)
	{
		if ((player != null) && !player.isInDuel())
		{
			if (_members.contains(player))
			{
				if (isLeader(player))
				{
					player.sendPacket(SystemMessageId.SLOW_DOWN_YOU_ARE_ALREADY_THE_PARTY_LEADER);
				}
				else
				{
					// Swap party members
					Player temp = getLeader();
					int p1 = _members.indexOf(player);
					_members.set(0, player);
					_members.set(p1, temp);
					SystemMessage msg = new SystemMessage(SystemMessageId.C1_HAS_BECOME_THE_PARTY_LEADER);
					msg.addString(getLeader().getName());
					broadcastPacket(msg);
					broadcastToPartyMembersNewLeader();
					if (isInCommandChannel() && _commandChannel.isLeader(temp))
					{
						_commandChannel.setLeader(getLeader());
						msg = new SystemMessage(SystemMessageId.COMMAND_CHANNEL_AUTHORITY_HAS_BEEN_TRANSFERRED_TO_C1);
						msg.addString(_commandChannel.getLeader().getName());
						_commandChannel.broadcastPacket(msg);
					}
				}
			}
			else
			{
				player.sendPacket(SystemMessageId.YOU_MAY_ONLY_TRANSFER_PARTY_LEADERSHIP_TO_ANOTHER_MEMBER_OF_THE_PARTY);
			}
		}
	}
	
	/**
	 * finds a player in the party by name
	 * @param name
	 * @return
	 */
	private Player getPlayerByName(String name)
	{
		foreach (Player member in _members)
		{
			if (member.getName().equalsIgnoreCase(name))
			{
				return member;
			}
		}
		return null;
	}
	
	/**
	 * distribute item(s) to party members
	 * @param player
	 * @param item
	 */
	public void distributeItem(Player player, Item item)
	{
		if (item.getId() == Inventory.ADENA_ID)
		{
			distributeAdena(player, item.getCount(), player);
			ItemData.getInstance().destroyItem("Party", item, player, null);
			return;
		}
		
		Player target = getActualLooter(player, item.getId(), false, player);
		target.addItem("Party", item, player, true);
		
		// Send messages to other party members about reward
		if (item.getCount() > 1)
		{
			SystemMessage msg = new SystemMessage(SystemMessageId.C1_HAS_OBTAINED_S2_X_S3);
			msg.addString(target.getName());
			msg.addItemName(item);
			msg.addLong(item.getCount());
			broadcastToPartyMembers(target, msg);
		}
		else
		{
			SystemMessage msg = new SystemMessage(SystemMessageId.C1_HAS_OBTAINED_S2);
			msg.addString(target.getName());
			msg.addItemName(item);
			broadcastToPartyMembers(target, msg);
		}
	}
	
	/**
	 * Distributes item loot between party members.
	 * @param player the reference player
	 * @param itemId the item ID
	 * @param itemCount the item count
	 * @param spoil {@code true} if it's spoil loot
	 * @param target the NPC target
	 */
	public void distributeItem(Player player, int itemId, long itemCount, bool spoil, Attackable target)
	{
		if (itemId == Inventory.ADENA_ID)
		{
			distributeAdena(player, itemCount, target);
			return;
		}
		
		Player looter = getActualLooter(player, itemId, spoil, target);
		looter.addItem(spoil ? "Sweeper Party" : "Party", itemId, itemCount, target, true);
		
		// Send messages to other party members about reward
		if (itemCount > 1)
		{
			SystemMessage msg = spoil ? new SystemMessage(SystemMessageId.C1_HAS_OBTAINED_S3_S2_S_BY_USING_SWEEPER) : new SystemMessage(SystemMessageId.C1_HAS_OBTAINED_S2_X_S3);
			msg.addString(looter.getName());
			msg.addItemName(itemId);
			msg.addLong(itemCount);
			broadcastToPartyMembers(looter, msg);
		}
		else
		{
			SystemMessage msg = spoil ? new SystemMessage(SystemMessageId.C1_HAS_OBTAINED_S2_BY_USING_SWEEPER) : new SystemMessage(SystemMessageId.C1_HAS_OBTAINED_S2);
			msg.addString(looter.getName());
			msg.addItemName(itemId);
			broadcastToPartyMembers(looter, msg);
		}
	}
	
	/**
	 * Method overload for {@link Party#distributeItem(Player, int, long, bool, Attackable)}
	 * @param player the reference player
	 * @param item the item holder
	 * @param spoil {@code true} if it's spoil loot
	 * @param target the NPC target
	 */
	public void distributeItem(Player player, ItemHolder item, bool spoil, Attackable target)
	{
		distributeItem(player, item.getId(), item.getCount(), spoil, target);
	}
	
	/**
	 * distribute adena to party members
	 * @param player
	 * @param adena
	 * @param target
	 */
	public void distributeAdena(Player player, long adena, Creature target)
	{
		// Check the number of party members that must be rewarded
		// (The party member must be in range to receive its reward)
		List<Player> toReward = new();
		foreach (Player member in _members)
		{
			if (Util.checkIfInRange(Config.ALT_PARTY_RANGE, target, member, true))
			{
				toReward.Add(member);
			}
		}
		
		if (!toReward.isEmpty())
		{
			// Now we can actually distribute the adena reward
			// (Total adena splitted by the number of party members that are in range and must be rewarded)
			long count = adena / toReward.Count;
			foreach (Player member in toReward)
			{
				member.addAdena("Party", count, player, true);
			}
		}
	}
	
	/**
	 * Distribute Experience and SP rewards to Player Party members in the known area of the last attacker.<br>
	 * <br>
	 * <b><u>Actions</u>:</b>
	 * <li>Get the Player owner of the Servitor (if necessary)</li>
	 * <li>Calculate the Experience and SP reward distribution rate</li>
	 * <li>Add Experience and SP to the Player</li><br>
	 * @param xpRewardValue The Experience reward to distribute
	 * @param spRewardValue The SP reward to distribute
	 * @param rewardedMembers The list of Player to reward
	 * @param topLvl
	 * @param target
	 */
	public void distributeXpAndSp(double xpRewardValue, double spRewardValue, List<Player> rewardedMembers, int topLvl, Attackable target)
	{
		List<Player> validMembers = getValidMembers(rewardedMembers, topLvl);
		double xpReward = xpRewardValue * getExpBonus(validMembers.Count, target.getInstanceWorld());
		double spReward = spRewardValue * getSpBonus(validMembers.Count, target.getInstanceWorld());
		int sqLevelSum = 0;
		foreach (Player member in validMembers)
		{
			sqLevelSum += (member.getLevel() * member.getLevel());
		}
		
		foreach (Player member in rewardedMembers)
		{
			if (member.isDead())
			{
				continue;
			}
			
			// Calculate and add the EXP and SP reward to the member
			if (validMembers.Contains(member))
			{
				// The servitor penalty
				float penalty = 1;
				
				foreach (Summon summon in member.getServitors().values())
				{
					if (((Servitor) summon).getExpMultiplier() > 1)
					{
						penalty = ((Servitor) summon).getExpMultiplier();
						break;
					}
				}
				
				double sqLevel = member.getLevel() * member.getLevel();
				double preCalculation = (sqLevel / sqLevelSum) * penalty;
				
				// Add the XP/SP points to the requested party member
				double exp = member.getStat().getValue(Stat.EXPSP_RATE, xpReward * preCalculation);
				double sp = member.getStat().getValue(Stat.EXPSP_RATE, spReward * preCalculation);
				exp = calculateExpSpPartyCutoff(member.getActingPlayer(), topLvl, exp, sp, target.useVitalityRate());
				if (exp > 0)
				{
					Clan clan = member.getClan();
					if (clan != null)
					{
						double finalExp = exp;
						
						if (target.useVitalityRate())
						
						{
							finalExp *= member.getStat().getExpBonusMultiplier();
						}
						clan.addHuntingPoints(member, target, finalExp);
					}
					member.updateVitalityPoints(target.getVitalityPoints(member.getLevel(), exp, target.isRaid()), true, false);
					PcCafePointsManager.getInstance().givePcCafePoint(member, exp);
					if (Config.ENABLE_MAGIC_LAMP)
					{
						MagicLampManager.getInstance().addLampExp(member, exp, true);
					}
					
					HuntPass huntpass = member.getHuntPass();
					if (huntpass != null)
					{
						huntpass.addPassPoint();
					}
					
					AchievementBox box = member.getAchievementBox();
					if (box != null)
					{
						member.getAchievementBox().addPoints(1);
					}
				}
			}
			else
			{
				member.addExpAndSp(0, 0);
			}
		}
	}
	
	private double calculateExpSpPartyCutoff(Player player, int topLvl, double addExpValue, double addSpValue, bool vit)
	{
		double addExp = addExpValue * Config.EXP_AMOUNT_MULTIPLIERS[player.getClassId().getId()];
		double addSp = addSpValue * Config.SP_AMOUNT_MULTIPLIERS[player.getClassId().getId()];
		
		// Premium rates
		if (player.hasPremiumStatus())
		{
			addExp *= Config.PREMIUM_RATE_XP;
			addSp *= Config.PREMIUM_RATE_SP;
		}
		
		double xp = addExp;
		double sp = addSp;
		if (Config.PARTY_XP_CUTOFF_METHOD.equalsIgnoreCase("highfive"))
		{
			int i = 0;
			int levelDiff = topLvl - player.getLevel();
			foreach (int[] gap in Config.PARTY_XP_CUTOFF_GAPS)
			{
				if ((levelDiff >= gap[0]) && (levelDiff <= gap[1]))
				{
					xp = (addExp * Config.PARTY_XP_CUTOFF_GAP_PERCENTS[i]) / 100;
					sp = (addSp * Config.PARTY_XP_CUTOFF_GAP_PERCENTS[i]) / 100;
					player.addExpAndSp(xp, sp, vit);
					break;
				}
				i++;
			}
		}
		else
		{
			player.addExpAndSp(addExp, addSp, vit);
		}
		return xp;
	}
	
	/**
	 * refresh party level
	 */
	public void recalculatePartyLevel()
	{
		int newLevel = 0;
		foreach (Player member in _members)
		{
			if (member == null)
			{
				_members.remove(member);
				continue;
			}
			
			if (member.getLevel() > newLevel)
			{
				newLevel = member.getLevel();
			}
		}
		_partyLvl = newLevel;
	}
	
	private List<Player> getValidMembers(List<Player> members, int topLvl)
	{
		List<Player> validMembers = new();
		switch (Config.PARTY_XP_CUTOFF_METHOD)
		{
			case "level":
			{
				foreach (Player member in members)
				{
					if ((topLvl - member.getLevel()) <= Config.PARTY_XP_CUTOFF_LEVEL)
					{
						validMembers.add(member);
					}
				}
				break;
			}
			case "percentage":
			{
				int sqLevelSum = 0;
				foreach (Player member in members)
				{
					sqLevelSum += (member.getLevel() * member.getLevel());
				}
				foreach (Player member in members)
				{
					int sqLevel = member.getLevel() * member.getLevel();
					if ((sqLevel * 100) >= (sqLevelSum * Config.PARTY_XP_CUTOFF_PERCENT))
					{
						validMembers.add(member);
					}
				}
				break;
			}
			case "auto":
			{
				int sqLevelSum = 0;
				foreach (Player member in members)
				{
					sqLevelSum += (member.getLevel() * member.getLevel());
				}
				int i = members.size() - 1;
				if (i < 1)
				{
					return members;
				}
				if (i >= BONUS_EXP_SP.length)
				{
					i = BONUS_EXP_SP.length - 1;
				}
				foreach (Player member in members)
				{
					final int sqLevel = member.getLevel() * member.getLevel();
					if (sqLevel >= (sqLevelSum / (members.size() * members.size())))
					{
						validMembers.add(member);
					}
				}
				break;
			}
			case "highfive":
			{
				validMembers.addAll(members);
				break;
			}
			case "none":
			{
				validMembers.addAll(members);
				break;
			}
		}
		return validMembers;
	}
	
	private double getBaseExpSpBonus(int membersCount)
	{
		int i = membersCount - 1;
		if (i < 1)
		{
			return 1;
		}
		if (i >= BONUS_EXP_SP.Length)
		{
			i = BONUS_EXP_SP.Length - 1;
		}
		return BONUS_EXP_SP[i];
	}
	
	private double getExpBonus(int membersCount, Instance instance)
	{
		float rateMul = instance != null ? instance.getExpPartyRate() : Config.RATE_PARTY_XP;
		return (membersCount < 2) ? (getBaseExpSpBonus(membersCount)) : (getBaseExpSpBonus(membersCount) * rateMul);
	}
	
	private double getSpBonus(int membersCount, Instance instance)
	{
		float rateMul = instance != null ? instance.getSPPartyRate() : Config.RATE_PARTY_SP;
		return (membersCount < 2) ? (getBaseExpSpBonus(membersCount)) : (getBaseExpSpBonus(membersCount) * rateMul);
	}
	
	public override int getLevel()
	{
		return _partyLvl;
	}
	
	public PartyDistributionType getDistributionType()
	{
		return _distributionType;
	}
	
	public bool isInCommandChannel()
	{
		return _commandChannel != null;
	}
	
	public CommandChannel getCommandChannel()
	{
		return _commandChannel;
	}
	
	public void setCommandChannel(CommandChannel channel)
	{
		_commandChannel = channel;
	}
	
	/**
	 * @return the leader of this party
	 */
	public override Player getLeader()
	{
		if (_members.isEmpty())
		{
			return null;
		}
		return _members.get(0);
	}
	
	[MethodImpl(MethodImplOptions.Synchronized)]
	public void requestLootChange(PartyDistributionType partyDistributionType)
	{
		if (_changeRequestDistributionType != null)
		{
			return;
		}
		_changeRequestDistributionType = partyDistributionType;
		_changeDistributionTypeAnswers = new HashSet<>();
		_changeDistributionTypeRequestTask = ThreadPool.schedule(() => finishLootRequest(false), PARTY_DISTRIBUTION_TYPE_REQUEST_TIMEOUT.toMillis());
		broadcastToPartyMembers(getLeader(), new ExAskModifyPartyLooting(getLeader().getName(), partyDistributionType));
		
		SystemMessage sm = new SystemMessage(SystemMessageId.REQUESTING_APPROVAL_FOR_CHANGING_PARTY_LOOT_TO_S1);
		sm.addSystemString(partyDistributionType.getSysStringId());
		getLeader().sendPacket(sm);
	}
	
	[MethodImpl(MethodImplOptions.Synchronized)]
	public void answerLootChangeRequest(Player member, bool answer)
	{
		if (_changeRequestDistributionType == null)
		{
			return;
		}
		
		if (_changeDistributionTypeAnswers.contains(member.getObjectId()))
		{
			return;
		}
		
		if (!answer)
		{
			finishLootRequest(false);
			return;
		}
		
		_changeDistributionTypeAnswers.add(member.getObjectId());
		if (_changeDistributionTypeAnswers.size() >= (getMemberCount() - 1))
		{
			finishLootRequest(true);
		}
	}
	
	[MethodImpl(MethodImplOptions.Synchronized)]
	protected void finishLootRequest(bool success)
	{
		if (_changeRequestDistributionType == null)
		{
			return;
		}
		if (_changeDistributionTypeRequestTask != null)
		{
			_changeDistributionTypeRequestTask.cancel(false);
			_changeDistributionTypeRequestTask = null;
		}
		if (success)
		{
			broadcastPacket(new ExSetPartyLooting(1, _changeRequestDistributionType));
			_distributionType = _changeRequestDistributionType;
			SystemMessage sm = new SystemMessage(SystemMessageId.PARTY_LOOTING_METHOD_WAS_CHANGED_TO_S1);
			sm.addSystemString(_changeRequestDistributionType.getSysStringId());
			broadcastPacket(sm);
		}
		else
		{
			broadcastPacket(new ExSetPartyLooting(0, _distributionType));
			broadcastPacket(new SystemMessage(SystemMessageId.PARTY_LOOT_CHANGE_WAS_CANCELLED));
		}
		_changeRequestDistributionType = null;
		_changeDistributionTypeAnswers = null;
	}
	
	/**
	 * @return a list of all members of this party
	 */
	public override List<Player> getMembers()
	{
		return _members;
	}
}