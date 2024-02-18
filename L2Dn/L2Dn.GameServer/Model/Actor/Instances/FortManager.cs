﻿using L2Dn.GameServer.Enums;
using L2Dn.GameServer.Model.Actor.Templates;
using L2Dn.GameServer.Model.Clans;
using L2Dn.GameServer.Model.Sieges;
using L2Dn.GameServer.Network.Enums;
using L2Dn.GameServer.Network.OutgoingPackets;
using L2Dn.GameServer.Utilities;

namespace L2Dn.GameServer.Model.Actor.Instances;

/**
 * Fortress Foreman implementation used for: Area Teleports, Support Magic, Clan Warehouse, Exp Loss Reduction
 */
public class FortManager : Merchant
{
	protected const int COND_ALL_FALSE = 0;
	protected const int COND_BUSY_BECAUSE_OF_SIEGE = 1;
	protected const int COND_OWNER = 2;
	
	public const int ORC_FORTRESS_ID = 122;
	
	public FortManager(NpcTemplate template): base(template)
	{
		setInstanceType(InstanceType.FortManager);
	}
	
	public override bool isWarehouse()
	{
		return true;
	}
	
	private void sendHtmlMessage(Player player, HtmlPacketHelper helper)
	{
		helper.Replace("%objectId%", getObjectId().ToString());
		helper.Replace("%npcId%", getId().ToString());
		
		NpcHtmlMessagePacket html = new NpcHtmlMessagePacket(getObjectId(), helper);
		player.sendPacket(html);
	}
	
	public override void onBypassFeedback(Player player, String command)
	{
		// BypassValidation Exploit plug.
		if (player.getLastFolkNPC().getObjectId() != getObjectId())
		{
			return;
		}
		 
		SimpleDateFormat format = new SimpleDateFormat("dd/MM/yyyy HH:mm");
		int condition = validateCondition(player);
		if (condition <= COND_ALL_FALSE)
		{
			return;
		}
		else if (condition == COND_BUSY_BECAUSE_OF_SIEGE)
		{
			return;
		}
		else if (condition == COND_OWNER)
		{
			 StringTokenizer st = new StringTokenizer(command, " ");
			 String actualCommand = st.nextToken(); // Get actual command
			String val = "";
			if (st.countTokens() >= 1)
			{
				val = st.nextToken();
			}
			if (actualCommand.equalsIgnoreCase("expel"))
			{
				if (player.hasClanPrivilege(ClanPrivilege.CS_DISMISS))
				{
					NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
					html.setFile(player, "data/html/fortress/foreman-expel.htm");
					html.replace("%objectId%", String.valueOf(getObjectId()));
					player.sendPacket(html);
				}
				else
				{
					NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
					html.setFile(player, "data/html/fortress/foreman-noprivs.htm");
					html.replace("%objectId%", String.valueOf(getObjectId()));
					player.sendPacket(html);
				}
				return;
			}
			else if (actualCommand.equalsIgnoreCase("banish_foreigner"))
			{
				if (player.hasClanPrivilege(ClanPrivilege.CS_DISMISS))
				{
					getFort().banishForeigners(); // Move non-clan members off fortress area
					 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
					html.setFile(player, "data/html/fortress/foreman-expeled.htm");
					html.replace("%objectId%", String.valueOf(getObjectId()));
					player.sendPacket(html);
				}
				else
				{
					 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
					html.setFile(player, "data/html/fortress/foreman-noprivs.htm");
					html.replace("%objectId%", String.valueOf(getObjectId()));
					player.sendPacket(html);
				}
				return;
			}
			else if (actualCommand.equalsIgnoreCase("receive_report"))
			{
				if (getFort().getFortState() < 2)
				{
					 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
					html.setFile(player, "data/html/fortress/foreman-report.htm");
					html.replace("%objectId%", String.valueOf(getObjectId()));
					if (Config.FS_MAX_OWN_TIME > 0)
					{
						 int hour = (int) Math.floor(getFort().getTimeTillRebelArmy() / 3600);
						 int minutes = (int) (Math.floor(getFort().getTimeTillRebelArmy() - (hour * 3600)) / 60);
						html.replace("%hr%", String.valueOf(hour));
						html.replace("%min%", String.valueOf(minutes));
					}
					else
					{
						 int hour = (int) Math.floor(getFort().getOwnedTime() / 3600);
						 int minutes = (int) (Math.floor(getFort().getOwnedTime() - (hour * 3600)) / 60);
						html.replace("%hr%", String.valueOf(hour));
						html.replace("%min%", String.valueOf(minutes));
					}
					player.sendPacket(html);
				}
				else
				{
					 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
					html.setFile(player, "data/html/fortress/foreman-castlereport.htm");
					html.replace("%objectId%", String.valueOf(getObjectId()));
					int hour;
					int minutes;
					if (Config.FS_MAX_OWN_TIME > 0)
					{
						hour = (int) Math.floor(getFort().getTimeTillRebelArmy() / 3600);
						minutes = (int) (Math.floor(getFort().getTimeTillRebelArmy() - (hour * 3600)) / 60);
						html.replace("%hr%", String.valueOf(hour));
						html.replace("%min%", String.valueOf(minutes));
					}
					else
					{
						hour = (int) Math.floor(getFort().getOwnedTime() / 3600);
						minutes = (int) (Math.floor(getFort().getOwnedTime() - (hour * 3600)) / 60);
						html.replace("%hr%", String.valueOf(hour));
						html.replace("%min%", String.valueOf(minutes));
					}
					hour = (int) Math.floor(getFort().getTimeTillNextFortUpdate() / 3600);
					minutes = (int) (Math.floor(getFort().getTimeTillNextFortUpdate() - (hour * 3600)) / 60);
					html.replace("%castle%", getFort().getContractedCastle().getName());
					html.replace("%hr2%", String.valueOf(hour));
					html.replace("%min2%", String.valueOf(minutes));
					player.sendPacket(html);
				}
				return;
			}
			else if (actualCommand.equalsIgnoreCase("operate_door")) // door
			// control
			{
				if (player.hasClanPrivilege(ClanPrivilege.CS_OPEN_DOOR))
				{
					if (!val.isEmpty())
					{
						 bool open = (int.Parse(val) == 1);
						while (st.hasMoreTokens())
						{
							getFort().openCloseDoor(player, int.Parse(st.nextToken()), open);
						}
						if (open)
						{
							if (getFort().getResidenceId() == ORC_FORTRESS_ID)
							{
								return;
							}
							
							 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
							html.setFile(player, "data/html/fortress/foreman-opened.htm");
							html.replace("%objectId%", String.valueOf(getObjectId()));
							player.sendPacket(html);
						}
						else
						{
							if (getFort().getResidenceId() == ORC_FORTRESS_ID)
							{
								return;
							}
							
							 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
							html.setFile(player, "data/html/fortress/foreman-closed.htm");
							html.replace("%objectId%", String.valueOf(getObjectId()));
							player.sendPacket(html);
						}
					}
					else
					{
						if (getFort().getResidenceId() == ORC_FORTRESS_ID)
						{
							return;
						}
						
						 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						html.setFile(player, "data/html/fortress/" + getTemplate().getId() + "-d.htm");
						html.replace("%objectId%", String.valueOf(getObjectId()));
						html.replace("%npcname%", getName());
						player.sendPacket(html);
					}
				}
				else
				{
					 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
					html.setFile(player, "data/html/fortress/foreman-noprivs.htm");
					html.replace("%objectId%", String.valueOf(getObjectId()));
					player.sendPacket(html);
				}
				return;
			}
			else if (actualCommand.equalsIgnoreCase("manage_vault"))
			{
				 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
				if (player.hasClanPrivilege(ClanPrivilege.CL_VIEW_WAREHOUSE))
				{
					if (val.equalsIgnoreCase("deposit"))
					{
						showVaultWindowDeposit(player);
					}
					else if (val.equalsIgnoreCase("withdraw"))
					{
						showVaultWindowWithdraw(player);
					}
					else
					{
						html.setFile(player, "data/html/fortress/foreman-vault.htm");
						sendHtmlMessage(player, html);
					}
				}
				else
				{
					html.setFile(player, "data/html/fortress/foreman-noprivs.htm");
					sendHtmlMessage(player, html);
				}
				return;
			}
			else if (actualCommand.equalsIgnoreCase("functions"))
			{
				if (val.equalsIgnoreCase("tele"))
				{
					 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
					if (getFort().getFortFunction(Fort.FUNC_TELEPORT) == null)
					{
						html.setFile(player, "data/html/fortress/foreman-nac.htm");
					}
					else
					{
						html.setFile(player, "data/html/fortress/" + getId() + "-t" + getFort().getFortFunction(Fort.FUNC_TELEPORT).getLvl() + ".htm");
					}
					sendHtmlMessage(player, html);
				}
				else if (val.equalsIgnoreCase("support"))
				{
					 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
					if (getFort().getFortFunction(Fort.FUNC_SUPPORT) == null)
					{
						html.setFile(player, "data/html/fortress/foreman-nac.htm");
					}
					else
					{
						html.setFile(player, "data/html/fortress/support" + getFort().getFortFunction(Fort.FUNC_SUPPORT).getLvl() + ".htm");
						html.replace("%mp%", String.valueOf((int) getCurrentMp()));
					}
					sendHtmlMessage(player, html);
				}
				else if (val.equalsIgnoreCase("back"))
				{
					showChatWindow(player);
				}
				else
				{
					 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
					html.setFile(player, "data/html/fortress/foreman-functions.htm");
					if (getFort().getFortFunction(Fort.FUNC_RESTORE_EXP) != null)
					{
						html.replace("%xp_regen%", String.valueOf(getFort().getFortFunction(Fort.FUNC_RESTORE_EXP).getLvl()));
					}
					else
					{
						html.replace("%xp_regen%", "0");
					}
					if (getFort().getFortFunction(Fort.FUNC_RESTORE_HP) != null)
					{
						html.replace("%hp_regen%", String.valueOf(getFort().getFortFunction(Fort.FUNC_RESTORE_HP).getLvl()));
					}
					else
					{
						html.replace("%hp_regen%", "0");
					}
					if (getFort().getFortFunction(Fort.FUNC_RESTORE_MP) != null)
					{
						html.replace("%mp_regen%", String.valueOf(getFort().getFortFunction(Fort.FUNC_RESTORE_MP).getLvl()));
					}
					else
					{
						html.replace("%mp_regen%", "0");
					}
					sendHtmlMessage(player, html);
				}
				return;
			}
			else if (actualCommand.equalsIgnoreCase("manage"))
			{
				if (player.hasClanPrivilege(ClanPrivilege.CS_SET_FUNCTIONS))
				{
					if (val.equalsIgnoreCase("recovery"))
					{
						if (st.countTokens() >= 1)
						{
							if (getFort().getOwnerClan() == null)
							{
								player.sendMessage("This fortress has no owner, you cannot change the configuration.");
								return;
							}
							val = st.nextToken();
							if (val.equalsIgnoreCase("hp_cancel"))
							{
								 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
								html.setFile(player, "data/html/fortress/functions-cancel.htm");
								html.replace("%apply%", "recovery hp 0");
								sendHtmlMessage(player, html);
								return;
							}
							else if (val.equalsIgnoreCase("mp_cancel"))
							{
								 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
								html.setFile(player, "data/html/fortress/functions-cancel.htm");
								html.replace("%apply%", "recovery mp 0");
								sendHtmlMessage(player, html);
								return;
							}
							else if (val.equalsIgnoreCase("exp_cancel"))
							{
								 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
								html.setFile(player, "data/html/fortress/functions-cancel.htm");
								html.replace("%apply%", "recovery exp 0");
								sendHtmlMessage(player, html);
								return;
							}
							else if (val.equalsIgnoreCase("edit_hp"))
							{
								val = st.nextToken();
								 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
								html.setFile(player, "data/html/fortress/functions-apply.htm");
								html.replace("%name%", "(HP Recovery Device)");
								 int percent = int.Parse(val);
								int cost;
								switch (percent)
								{
									case 300:
									{
										cost = Config.FS_HPREG1_FEE;
										break;
									}
									default: // 400
									{
										cost = Config.FS_HPREG2_FEE;
										break;
									}
								}
								
								html.replace("%cost%", cost + "</font>Adena /" + (Config.FS_HPREG_FEE_RATIO / 1000 / 60 / 60 / 24) + " Day</font>)");
								html.replace("%use%", "Provides additional HP recovery for clan members in the fortress.<font color=\"00FFFF\">" + percent + "%</font>");
								html.replace("%apply%", "recovery hp " + percent);
								sendHtmlMessage(player, html);
								return;
							}
							else if (val.equalsIgnoreCase("edit_mp"))
							{
								val = st.nextToken();
								 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
								html.setFile(player, "data/html/fortress/functions-apply.htm");
								html.replace("%name%", "(MP Recovery)");
								 int percent = int.Parse(val);
								int cost;
								switch (percent)
								{
									case 40:
									{
										cost = Config.FS_MPREG1_FEE;
										break;
									}
									default: // 50
									{
										cost = Config.FS_MPREG2_FEE;
										break;
									}
								}
								html.replace("%cost%", cost + "</font>Adena /" + (Config.FS_MPREG_FEE_RATIO / 1000 / 60 / 60 / 24) + " Day</font>)");
								html.replace("%use%", "Provides additional MP recovery for clan members in the fortress.<font color=\"00FFFF\">" + percent + "%</font>");
								html.replace("%apply%", "recovery mp " + percent);
								sendHtmlMessage(player, html);
								return;
							}
							else if (val.equalsIgnoreCase("edit_exp"))
							{
								val = st.nextToken();
								 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
								html.setFile(player, "data/html/fortress/functions-apply.htm");
								html.replace("%name%", "(EXP Recovery Device)");
								 int percent = int.Parse(val);
								int cost;
								switch (percent)
								{
									case 45:
									{
										cost = Config.FS_EXPREG1_FEE;
										break;
									}
									default: // 50
									{
										cost = Config.FS_EXPREG2_FEE;
										break;
									}
								}
								html.replace("%cost%", cost + "</font>Adena /" + (Config.FS_EXPREG_FEE_RATIO / 1000 / 60 / 60 / 24) + " Day</font>)");
								html.replace("%use%", "Restores the Exp of any clan member who is resurrected in the fortress.<font color=\"00FFFF\">" + percent + "%</font>");
								html.replace("%apply%", "recovery exp " + percent);
								sendHtmlMessage(player, html);
								return;
							}
							else if (val.equalsIgnoreCase("hp"))
							{
								if (st.countTokens() >= 1)
								{
									int fee;
									val = st.nextToken();
									 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
									html.setFile(player, "data/html/fortress/functions-apply_confirmed.htm");
									if (getFort().getFortFunction(Fort.FUNC_RESTORE_HP) != null)
									{
										if (getFort().getFortFunction(Fort.FUNC_RESTORE_HP).getLvl() == int.Parse(val))
										{
											html.setFile(player, "data/html/fortress/functions-used.htm");
											html.replace("%val%", val + "%");
											sendHtmlMessage(player, html);
											return;
										}
									}
									 int percent = int.Parse(val);
									switch (percent)
									{
										case 0:
										{
											fee = 0;
											html.setFile(player, "data/html/fortress/functions-cancel_confirmed.htm");
											break;
										}
										case 300:
										{
											fee = Config.FS_HPREG1_FEE;
											break;
										}
										default: // 400
										{
											fee = Config.FS_HPREG2_FEE;
											break;
										}
									}
									if (!getFort().updateFunctions(player, Fort.FUNC_RESTORE_HP, percent, fee, Config.FS_HPREG_FEE_RATIO, (getFort().getFortFunction(Fort.FUNC_RESTORE_HP) == null)))
									{
										html.setFile(player, "data/html/fortress/low_adena.htm");
										sendHtmlMessage(player, html);
									}
									sendHtmlMessage(player, html);
								}
								return;
							}
							else if (val.equalsIgnoreCase("mp"))
							{
								if (st.countTokens() >= 1)
								{
									int fee;
									val = st.nextToken();
									 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
									html.setFile(player, "data/html/fortress/functions-apply_confirmed.htm");
									if (getFort().getFortFunction(Fort.FUNC_RESTORE_MP) != null)
									{
										if (getFort().getFortFunction(Fort.FUNC_RESTORE_MP).getLvl() == int.Parse(val))
										{
											html.setFile(player, "data/html/fortress/functions-used.htm");
											html.replace("%val%", val + "%");
											sendHtmlMessage(player, html);
											return;
										}
									}
									 int percent = int.Parse(val);
									switch (percent)
									{
										case 0:
										{
											fee = 0;
											html.setFile(player, "data/html/fortress/functions-cancel_confirmed.htm");
											break;
										}
										case 40:
										{
											fee = Config.FS_MPREG1_FEE;
											break;
										}
										default: // 50
										{
											fee = Config.FS_MPREG2_FEE;
											break;
										}
									}
									if (!getFort().updateFunctions(player, Fort.FUNC_RESTORE_MP, percent, fee, Config.FS_MPREG_FEE_RATIO, (getFort().getFortFunction(Fort.FUNC_RESTORE_MP) == null)))
									{
										html.setFile(player, "data/html/fortress/low_adena.htm");
										sendHtmlMessage(player, html);
									}
									sendHtmlMessage(player, html);
								}
								return;
							}
							else if (val.equalsIgnoreCase("exp"))
							{
								if (st.countTokens() >= 1)
								{
									int fee;
									val = st.nextToken();
									 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
									html.setFile(player, "data/html/fortress/functions-apply_confirmed.htm");
									if (getFort().getFortFunction(Fort.FUNC_RESTORE_EXP) != null)
									{
										if (getFort().getFortFunction(Fort.FUNC_RESTORE_EXP).getLvl() == int.Parse(val))
										{
											html.setFile(player, "data/html/fortress/functions-used.htm");
											html.replace("%val%", val + "%");
											sendHtmlMessage(player, html);
											return;
										}
									}
									 int percent = int.Parse(val);
									switch (percent)
									{
										case 0:
										{
											fee = 0;
											html.setFile(player, "data/html/fortress/functions-cancel_confirmed.htm");
											break;
										}
										case 45:
										{
											fee = Config.FS_EXPREG1_FEE;
											break;
										}
										default: // 50
										{
											fee = Config.FS_EXPREG2_FEE;
											break;
										}
									}
									if (!getFort().updateFunctions(player, Fort.FUNC_RESTORE_EXP, percent, fee, Config.FS_EXPREG_FEE_RATIO, (getFort().getFortFunction(Fort.FUNC_RESTORE_EXP) == null)))
									{
										html.setFile(player, "data/html/fortress/low_adena.htm");
										sendHtmlMessage(player, html);
									}
									sendHtmlMessage(player, html);
								}
								return;
							}
						}
						 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						html.setFile(player, "data/html/fortress/edit_recovery.htm");
						 String hp = "[<a action=\"bypass -h npc_%objectId%_manage recovery edit_hp 300\">300%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_hp 400\">400%</a>]";
						 String exp = "[<a action=\"bypass -h npc_%objectId%_manage recovery edit_exp 45\">45%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_exp 50\">50%</a>]";
						 String mp = "[<a action=\"bypass -h npc_%objectId%_manage recovery edit_mp 40\">40%</a>][<a action=\"bypass -h npc_%objectId%_manage recovery edit_mp 50\">50%</a>]";
						if (getFort().getFortFunction(Fort.FUNC_RESTORE_HP) != null)
						{
							html.replace("%hp_recovery%", getFort().getFortFunction(Fort.FUNC_RESTORE_HP).getLvl() + "%</font> (<font color=\"FFAABB\">" + getFort().getFortFunction(Fort.FUNC_RESTORE_HP).getLease() + "</font>Adena /" + (Config.FS_HPREG_FEE_RATIO / 1000 / 60 / 60 / 24) + " Day)");
							html.replace("%hp_period%", "Withdraw the fee for the next time at " + format.format(getFort().getFortFunction(Fort.FUNC_RESTORE_HP).getEndTime()));
							html.replace("%change_hp%", "[<a action=\"bypass -h npc_%objectId%_manage recovery hp_cancel\">Deactivate</a>]" + hp);
						}
						else
						{
							html.replace("%hp_recovery%", "none");
							html.replace("%hp_period%", "none");
							html.replace("%change_hp%", hp);
						}
						if (getFort().getFortFunction(Fort.FUNC_RESTORE_EXP) != null)
						{
							html.replace("%exp_recovery%", getFort().getFortFunction(Fort.FUNC_RESTORE_EXP).getLvl() + "%</font> (<font color=\"FFAABB\">" + getFort().getFortFunction(Fort.FUNC_RESTORE_EXP).getLease() + "</font>Adena /" + (Config.FS_EXPREG_FEE_RATIO / 1000 / 60 / 60 / 24) + " Day)");
							html.replace("%exp_period%", "Withdraw the fee for the next time at " + format.format(getFort().getFortFunction(Fort.FUNC_RESTORE_EXP).getEndTime()));
							html.replace("%change_exp%", "[<a action=\"bypass -h npc_%objectId%_manage recovery exp_cancel\">Deactivate</a>]" + exp);
						}
						else
						{
							html.replace("%exp_recovery%", "none");
							html.replace("%exp_period%", "none");
							html.replace("%change_exp%", exp);
						}
						if (getFort().getFortFunction(Fort.FUNC_RESTORE_MP) != null)
						{
							html.replace("%mp_recovery%", getFort().getFortFunction(Fort.FUNC_RESTORE_MP).getLvl() + "%</font> (<font color=\"FFAABB\">" + getFort().getFortFunction(Fort.FUNC_RESTORE_MP).getLease() + "</font>Adena /" + (Config.FS_MPREG_FEE_RATIO / 1000 / 60 / 60 / 24) + " Day)");
							html.replace("%mp_period%", "Withdraw the fee for the next time at " + format.format(getFort().getFortFunction(Fort.FUNC_RESTORE_MP).getEndTime()));
							html.replace("%change_mp%", "[<a action=\"bypass -h npc_%objectId%_manage recovery mp_cancel\">Deactivate</a>]" + mp);
						}
						else
						{
							html.replace("%mp_recovery%", "none");
							html.replace("%mp_period%", "none");
							html.replace("%change_mp%", mp);
						}
						sendHtmlMessage(player, html);
					}
					else if (val.equalsIgnoreCase("other"))
					{
						if (st.countTokens() >= 1)
						{
							if (getFort().getOwnerClan() == null)
							{
								player.sendMessage("This fortress has no owner, you cannot change the configuration.");
								return;
							}
							val = st.nextToken();
							if (val.equalsIgnoreCase("tele_cancel"))
							{
								 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
								html.setFile(player, "data/html/fortress/functions-cancel.htm");
								html.replace("%apply%", "other tele 0");
								sendHtmlMessage(player, html);
								return;
							}
							else if (val.equalsIgnoreCase("support_cancel"))
							{
								 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
								html.setFile(player, "data/html/fortress/functions-cancel.htm");
								html.replace("%apply%", "other support 0");
								sendHtmlMessage(player, html);
								return;
							}
							else if (val.equalsIgnoreCase("edit_support"))
							{
								val = st.nextToken();
								 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
								html.setFile(player, "data/html/fortress/functions-apply.htm");
								html.replace("%name%", "Insignia (Supplementary Magic)");
								 int stage = int.Parse(val);
								int cost;
								switch (stage)
								{
									case 1:
									{
										cost = Config.FS_SUPPORT1_FEE;
										break;
									}
									default:
									{
										cost = Config.FS_SUPPORT2_FEE;
										break;
									}
								}
								html.replace("%cost%", cost + "</font>Adena /" + (Config.FS_SUPPORT_FEE_RATIO / 1000 / 60 / 60 / 24) + " Day</font>)");
								html.replace("%use%", "Enables the use of supplementary magic.");
								html.replace("%apply%", "other support " + stage);
								sendHtmlMessage(player, html);
								return;
							}
							else if (val.equalsIgnoreCase("edit_tele"))
							{
								val = st.nextToken();
								 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
								html.setFile(player, "data/html/fortress/functions-apply.htm");
								html.replace("%name%", "Mirror (Teleportation Device)");
								 int stage = int.Parse(val);
								int cost;
								switch (stage)
								{
									case 1:
									{
										cost = Config.FS_TELE1_FEE;
										break;
									}
									default:
									{
										cost = Config.FS_TELE2_FEE;
										break;
									}
								}
								html.replace("%cost%", cost + "</font>Adena /" + (Config.FS_TELE_FEE_RATIO / 1000 / 60 / 60 / 24) + " Day</font>)");
								html.replace("%use%", "Teleports clan members in a fort to the target <font color=\"00FFFF\">Stage " + stage + "</font> staging area");
								html.replace("%apply%", "other tele " + stage);
								sendHtmlMessage(player, html);
								return;
							}
							else if (val.equalsIgnoreCase("tele"))
							{
								if (st.countTokens() >= 1)
								{
									int fee;
									val = st.nextToken();
									 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
									html.setFile(player, "data/html/fortress/functions-apply_confirmed.htm");
									if (getFort().getFortFunction(Fort.FUNC_TELEPORT) != null)
									{
										if (getFort().getFortFunction(Fort.FUNC_TELEPORT).getLvl() == int.Parse(val))
										{
											html.setFile(player, "data/html/fortress/functions-used.htm");
											html.replace("%val%", "Stage " + val);
											sendHtmlMessage(player, html);
											return;
										}
									}
									 int level = int.Parse(val);
									switch (level)
									{
										case 0:
										{
											fee = 0;
											html.setFile(player, "data/html/fortress/functions-cancel_confirmed.htm");
											break;
										}
										case 1:
										{
											fee = Config.FS_TELE1_FEE;
											break;
										}
										default:
										{
											fee = Config.FS_TELE2_FEE;
											break;
										}
									}
									if (!getFort().updateFunctions(player, Fort.FUNC_TELEPORT, level, fee, Config.FS_TELE_FEE_RATIO, (getFort().getFortFunction(Fort.FUNC_TELEPORT) == null)))
									{
										html.setFile(player, "data/html/fortress/low_adena.htm");
										sendHtmlMessage(player, html);
									}
									sendHtmlMessage(player, html);
								}
								return;
							}
							else if (val.equalsIgnoreCase("support"))
							{
								if (st.countTokens() >= 1)
								{
									int fee;
									val = st.nextToken();
									 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
									html.setFile(player, "data/html/fortress/functions-apply_confirmed.htm");
									if (getFort().getFortFunction(Fort.FUNC_SUPPORT) != null)
									{
										if (getFort().getFortFunction(Fort.FUNC_SUPPORT).getLvl() == int.Parse(val))
										{
											html.setFile(player, "data/html/fortress/functions-used.htm");
											html.replace("%val%", "Stage " + val);
											sendHtmlMessage(player, html);
											return;
										}
									}
									 int level = int.Parse(val);
									switch (level)
									{
										case 0:
										{
											fee = 0;
											html.setFile(player, "data/html/fortress/functions-cancel_confirmed.htm");
											break;
										}
										case 1:
										{
											fee = Config.FS_SUPPORT1_FEE;
											break;
										}
										default:
										{
											fee = Config.FS_SUPPORT2_FEE;
											break;
										}
									}
									if (!getFort().updateFunctions(player, Fort.FUNC_SUPPORT, level, fee, Config.FS_SUPPORT_FEE_RATIO, (getFort().getFortFunction(Fort.FUNC_SUPPORT) == null)))
									{
										html.setFile(player, "data/html/fortress/low_adena.htm");
										sendHtmlMessage(player, html);
									}
									else
									{
										sendHtmlMessage(player, html);
									}
								}
								return;
							}
						}
						 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						html.setFile(player, "data/html/fortress/edit_other.htm");
						 String tele = "[<a action=\"bypass -h npc_%objectId%_manage other edit_tele 1\">Level 1</a>][<a action=\"bypass -h npc_%objectId%_manage other edit_tele 2\">Level 2</a>]";
						 String support = "[<a action=\"bypass -h npc_%objectId%_manage other edit_support 1\">Level 1</a>][<a action=\"bypass -h npc_%objectId%_manage other edit_support 2\">Level 2</a>]";
						if (getFort().getFortFunction(Fort.FUNC_TELEPORT) != null)
						{
							html.replace("%tele%", "Stage " + getFort().getFortFunction(Fort.FUNC_TELEPORT).getLvl() + "</font> (<font color=\"FFAABB\">" + getFort().getFortFunction(Fort.FUNC_TELEPORT).getLease() + "</font>Adena /" + (Config.FS_TELE_FEE_RATIO / 1000 / 60 / 60 / 24) + " Day)");
							html.replace("%tele_period%", "Withdraw the fee for the next time at " + format.format(getFort().getFortFunction(Fort.FUNC_TELEPORT).getEndTime()));
							html.replace("%change_tele%", "[<a action=\"bypass -h npc_%objectId%_manage other tele_cancel\">Deactivate</a>]" + tele);
						}
						else
						{
							html.replace("%tele%", "none");
							html.replace("%tele_period%", "none");
							html.replace("%change_tele%", tele);
						}
						if (getFort().getFortFunction(Fort.FUNC_SUPPORT) != null)
						{
							html.replace("%support%", "Stage " + getFort().getFortFunction(Fort.FUNC_SUPPORT).getLvl() + "</font> (<font color=\"FFAABB\">" + getFort().getFortFunction(Fort.FUNC_SUPPORT).getLease() + "</font>Adena /" + (Config.FS_SUPPORT_FEE_RATIO / 1000 / 60 / 60 / 24) + " Day)");
							html.replace("%support_period%", "Withdraw the fee for the next time at " + format.format(getFort().getFortFunction(Fort.FUNC_SUPPORT).getEndTime()));
							html.replace("%change_support%", "[<a action=\"bypass -h npc_%objectId%_manage other support_cancel\">Deactivate</a>]" + support);
						}
						else
						{
							html.replace("%support%", "none");
							html.replace("%support_period%", "none");
							html.replace("%change_support%", support);
						}
						sendHtmlMessage(player, html);
					}
					else if (val.equalsIgnoreCase("back"))
					{
						showChatWindow(player);
					}
					else
					{
						 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						html.setFile(player, "data/html/fortress/manage.htm");
						sendHtmlMessage(player, html);
					}
				}
				else
				{
					 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
					html.setFile(player, "data/html/fortress/foreman-noprivs.htm");
					sendHtmlMessage(player, html);
				}
				return;
			}
			else if (actualCommand.equalsIgnoreCase("support"))
			{
				setTarget(player);
				Skill skill;
				if (val.isEmpty())
				{
					return;
				}
				
				try
				{
					 int skillId = int.Parse(val);
					try
					{
						if (getFort().getFortFunction(Fort.FUNC_SUPPORT) == null)
						{
							return;
						}
						if (getFort().getFortFunction(Fort.FUNC_SUPPORT).getLvl() == 0)
						{
							return;
						}
						 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
						int skillLevel = 0;
						if (st.countTokens() >= 1)
						{
							skillLevel = int.Parse(st.nextToken());
						}
						skill = SkillData.getInstance().getSkill(skillId, skillLevel);
						if (skill.hasEffectType(EffectType.SUMMON))
						{
							player.doCast(skill);
						}
						else if ((skill.getMpConsume() + skill.getMpInitialConsume()) <= getCurrentMp())
						{
							doCast(skill);
						}
						else
						{
							html.setFile(player, "data/html/fortress/support-no_mana.htm");
							html.replace("%mp%", String.valueOf((int) getCurrentMp()));
							sendHtmlMessage(player, html);
							return;
						}
						html.setFile(player, "data/html/fortress/support-done.htm");
						html.replace("%mp%", String.valueOf((int) getCurrentMp()));
						sendHtmlMessage(player, html);
					}
					catch (Exception e)
					{
						player.sendMessage("Invalid skill level, contact your admin!");
					}
				}
				catch (Exception e)
				{
					player.sendMessage("Invalid skill level, contact your admin!");
				}
				return;
			}
			else if (actualCommand.equalsIgnoreCase("support_back"))
			{
				 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
				if (getFort().getFortFunction(Fort.FUNC_SUPPORT).getLvl() == 0)
				{
					return;
				}
				html.setFile(player, "data/html/fortress/support" + getFort().getFortFunction(Fort.FUNC_SUPPORT).getLvl() + ".htm");
				html.replace("%mp%", String.valueOf((int) getStatus().getCurrentMp()));
				sendHtmlMessage(player, html);
				return;
			}
			else if (actualCommand.equalsIgnoreCase("goto")) // goto listId locId
			{
				 FortFunction func = getFort().getFortFunction(Fort.FUNC_TELEPORT);
				if ((func == null) || !st.hasMoreTokens())
				{
					return;
				}
				
				 int funcLvl = (val.length() >= 4) ? CommonUtil.parseInt(val.substring(3), -1) : -1;
				if (func.getLvl() == funcLvl)
				{
					final TeleportHolder holder = TeleporterData.getInstance().getHolder(getId(), val);
					if (holder != null)
					{
						holder.doTeleport(player, this, CommonUtil.parseNextInt(st, -1));
					}
				}
				return;
			}
			base.onBypassFeedback(player, command);
		}
	}
	
	public override void showChatWindow(Player player)
	{
		player.sendPacket(ActionFailedPacket.STATIC_PACKET);
		String filename = "data/html/fortress/foreman-no.htm";
		
		 int condition = validateCondition(player);
		if (condition > COND_ALL_FALSE)
		{
			if (condition == COND_BUSY_BECAUSE_OF_SIEGE)
			{
				filename = "data/html/fortress/foreman-busy.htm"; // Busy because of siege
			}
			else if (condition == COND_OWNER)
			{
				filename = "data/html/fortress/foreman.htm"; // Owner message window
			}
		}
		
		 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
		html.setFile(player, filename);
		html.replace("%objectId%", String.valueOf(getObjectId()));
		html.replace("%npcname%", getName());
		player.sendPacket(html);
	}
	
	protected int validateCondition(Player player)
	{
		if ((getFort() != null) && (getFort().getResidenceId() > 0) && (player.getClan() != null))
		{
			if (getFort().getZone().isActive())
			{
				return COND_BUSY_BECAUSE_OF_SIEGE; // Busy because of siege
			}
			else if ((getFort().getOwnerClan() != null) && (getFort().getOwnerClan().getId() == player.getClanId()))
			{
				return COND_OWNER; // Owner
			}
		}
		return COND_ALL_FALSE;
	}
	
	private void showVaultWindowDeposit(Player player)
	{
		player.sendPacket(ActionFailedPacket.STATIC_PACKET);
		player.setActiveWarehouse(player.getClan().getWarehouse());
		player.sendPacket(new WareHouseDepositList(1, player, WareHouseDepositList.CLAN));
	}
	
	private void showVaultWindowWithdraw(Player player)
	{
		if (player.isClanLeader() || player.hasClanPrivilege(ClanPrivilege.CL_VIEW_WAREHOUSE))
		{
			player.sendPacket(ActionFailedPacket.STATIC_PACKET);
			player.setActiveWarehouse(player.getClan().getWarehouse());
			player.sendPacket(new WareHouseWithdrawalList(1, player, WareHouseWithdrawalList.CLAN));
		}
		else
		{
			 NpcHtmlMessage html = new NpcHtmlMessage(getObjectId());
			html.setFile(player, "data/html/fortress/foreman-noprivs.htm");
			sendHtmlMessage(player, html);
		}
	}
}