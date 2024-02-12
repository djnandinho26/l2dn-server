﻿using L2Dn.GameServer.Data.Xml;
using L2Dn.GameServer.Enums;
using L2Dn.GameServer.InstanceManagers;
using L2Dn.GameServer.Model.Actor.Templates;
using L2Dn.GameServer.Model.Holders;
using L2Dn.GameServer.Model.Quests;
using L2Dn.GameServer.Model.Teleporters;
using L2Dn.GameServer.Utilities;
using NLog;

namespace L2Dn.GameServer.Model.Actor.Instances;

public class Teleporter: Npc
{
	private static readonly Logger LOGGER = LogManager.GetLogger(nameof(Teleporter));
	
	private static readonly Map<int, List<TeleporterQuestRecommendationHolder>> QUEST_RECOMENDATIONS = new();
	// static
	// {
	// QUEST_RECOMENDATIONS.put(30848, new ArrayList<>());
	// QUEST_RECOMENDATIONS.get(30848).add(new TeleporterQuestRecommendationHolder(30848, "Q00561_BasicMissionHarnakUndergroundRuins", new int[]{-1}, "30848-Q561-Q562"));
	// QUEST_RECOMENDATIONS.get(30848).add(new TeleporterQuestRecommendationHolder(30848, "Q00562_BasicMissionAltarOfEvil", new int[]{-1}, "30848-561-562"));
	// }
	
	public Teleporter(NpcTemplate template): base(template)
	{
		setInstanceType(InstanceType.Teleporter);
	}
	
	public override bool isAutoAttackable(Creature attacker)
	{
		return attacker.isMonster() || base.isAutoAttackable(attacker);
	}
	
	public override void onBypassFeedback(Player player, String command)
	{
		// Process bypass
		StringTokenizer st = new StringTokenizer(command, " ");
		switch (st.nextToken())
		{
			case "showNoblesSelect":
			{
				sendHtmlMessage(player, "data/html/teleporter/" + (player.isNoble() ? "nobles_select" : "not_nobles") + ".htm");
				break;
			}
			case "showTeleports":
			{
				String listName = (st.hasMoreTokens()) ? st.nextToken() : TeleportType.NORMAL.name();
				TeleportHolder holder = TeleporterData.getInstance().getHolder(getId(), listName);
				if (holder == null)
				{
					LOGGER.Warn(player + " requested show teleports for list with name " + listName + " at NPC " + getId() + "!");
					return;
				}
				holder.showTeleportList(player, this);
				break;
			}
			case "showTeleportList":
			{
				player.sendPacket(ExShowTeleportUi.STATIC_PACKET);
				break;
			}
			case "showTeleportsHunting":
			{
				String listName = (st.hasMoreTokens()) ? st.nextToken() : TeleportType.HUNTING.name();
				TeleportHolder holder = TeleporterData.getInstance().getHolder(getId(), listName);
				if (holder == null)
				{
					LOGGER.Warn(player + " requested show teleports for hunting list with name " + listName + " at NPC " + getId() + "!");
					return;
				}
				holder.showTeleportList(player, this);
				break;
			}
			case "teleport":
			{
				// Check for required count of params.
				if (st.countTokens() != 2)
				{
					LOGGER.Warn(player + " send unhandled teleport command: " + command);
					return;
				}
				
				String listName = st.nextToken();
				TeleportHolder holder = TeleporterData.getInstance().getHolder(getId(), listName);
				if (holder == null)
				{
					LOGGER.Warn(player + " requested unknown teleport list: " + listName + " for npc: " + getId() + "!");
					return;
				}
				holder.doTeleport(player, this, parseNextInt(st, -1));
				break;
			}
			case "chat":
			{
				int val = 0;
				try
				{
					val = Integer.parseInt(command.substring(5));
				}
				catch (IndexOutOfRangeException)
				{
				}
				catch (FormatException)
				{
				}
				showChatWindow(player, val);
				break;
			}
			default:
			{
				base.onBypassFeedback(player, command);
				break;
			}
		}
	}
	
	private int parseNextInt(StringTokenizer st, int defaultVal)
	{
		if (st.hasMoreTokens())
		{
			String token = st.nextToken();
			if (Util.isDigit(token))
			{
				return Integer.parseInt(token);
			}
		}
		return defaultVal;
	}
	
	public override String getHtmlPath(int npcId, int value, Player player)
	{
		String pom;
		if (value == 0)
		{
			pom = String.valueOf(npcId);
			if ((player != null) && QUEST_RECOMENDATIONS.containsKey(npcId))
			{
				CHECK: foreach (TeleporterQuestRecommendationHolder rec in QUEST_RECOMENDATIONS.get(npcId))
				{
					QuestState qs = player.getQuestState(rec.getQuestName());
					if ((qs != null) && qs.isStarted())
					{
						foreach (int cond in rec.getConditions())
						{
							if ((cond == -1) || qs.isCond(cond))
							{
								pom = rec.getHtml();
								break CHECK;
							}
						}
					}
				}
			}
		}
		else
		{
			pom = (npcId + "-" + value);
		}
		return "data/html/teleporter/" + pom + ".htm";
	}
	
	public override void showChatWindow(Player player)
	{
		// Teleporter isn't on castle ground
		if (CastleManager.getInstance().getCastle(this) == null)
		{
			base.showChatWindow(player);
			return;
		}
		
		// Teleporter is on castle ground
		String filename = "data/html/teleporter/castleteleporter-no.htm";
		if ((player.getClan() != null) && (getCastle().getOwnerId() == player.getClanId())) // Clan owns castle
		{
			filename = getHtmlPath(getId(), 0, player); // Owner message window
		}
		else if (getCastle().getSiege().isInProgress()) // Teleporter is busy due siege
		{
			filename = "data/html/teleporter/castleteleporter-busy.htm"; // Busy because of siege
		}
		sendHtmlMessage(player, filename);
	}
	
	private void sendHtmlMessage(Player player, String filename)
	{
		NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
		html.setFile(player, filename);
		html.replace("%objectId%", String.valueOf(getObjectId()));
		html.replace("%npcname%", getName());
		player.sendPacket(html);
	}
}