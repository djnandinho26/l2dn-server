using System.Runtime.CompilerServices;
using L2Dn.GameServer.Db;
using L2Dn.GameServer.Enums;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Holders;
using L2Dn.GameServer.Network;
using L2Dn.GameServer.Network.OutgoingPackets;
using L2Dn.GameServer.Utilities;
using L2Dn.Utilities;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace L2Dn.GameServer.Data.Sql;

public class OfflineTraderTable
{
	private static readonly Logger LOGGER = LogManager.GetLogger(nameof(OfflineTraderTable));

	protected OfflineTraderTable()
	{
	}

	public void storeOffliners()
	{
		try
		{
			using GameServerDbContext ctx = DbFactory.Instance.CreateDbContext();
			ctx.CharacterOfflineTradeItems.ExecuteDelete();
			ctx.CharacterOfflineTrades.ExecuteDelete();
			foreach (Player pc in World.getInstance().getPlayers())
			{
				try
				{
					if ((pc.getPrivateStoreType() != PrivateStoreType.NONE) &&
					    ((pc.getClient() == null) || pc.getClient().IsDetached))
					{
						var trade = new CharacterOfflineTrade
						{
							CharacterId = pc.getObjectId(), // Char Id
							Time = pc.getOfflineStartTime() ?? DateTime.UtcNow,
							Type = (byte)(pc.isSellingBuffs() ? PrivateStoreType.SELL_BUFFS : pc.getPrivateStoreType()),
						};

						switch (pc.getPrivateStoreType())
						{
							case PrivateStoreType.BUY:
							{
								if (!Config.OFFLINE_TRADE_ENABLE)
								{
									continue;
								}

								trade.Title = pc.getBuyList().getTitle();
								ctx.CharacterOfflineTradeItems.AddRange(pc.getBuyList().getItems().Select(i =>
									new CharacterOfflineTradeItem
									{
										CharacterId = pc.getObjectId(),
										ItemId = i.getItem().getId(),
										Count = i.getCount(),
										Price = i.getPrice()
									}));

								break;
							}
							case PrivateStoreType.SELL:
							case PrivateStoreType.PACKAGE_SELL:
							{
								if (!Config.OFFLINE_TRADE_ENABLE)
								{
									continue;
								}

								trade.Title = pc.getSellList().getTitle();
								if (pc.isSellingBuffs())
								{
									ctx.CharacterOfflineTradeItems.AddRange(pc.getSellingBuffs().Select(holder =>
										new CharacterOfflineTradeItem
										{
											CharacterId = pc.getObjectId(),
											ItemId = holder.getSkillId(),
											Count = 0,
											Price = holder.getPrice()
										}));
								}
								else
								{
									ctx.CharacterOfflineTradeItems.AddRange(pc.getSellList().getItems().Select(i =>
										new CharacterOfflineTradeItem
										{
											CharacterId = pc.getObjectId(),
											ItemId = i.getObjectId(),
											Count = i.getCount(),
											Price = i.getPrice()
										}));
								}

								break;
							}
							case PrivateStoreType.MANUFACTURE:
							{
								if (!Config.OFFLINE_CRAFT_ENABLE)
								{
									continue;
								}

								trade.Title = pc.getStoreName();
								ctx.CharacterOfflineTradeItems.AddRange(pc.getManufactureItems().values().Select(i =>
									new CharacterOfflineTradeItem
									{
										CharacterId = pc.getObjectId(),
										ItemId = i.getRecipeId(),
										Count = 0,
										Price = i.getCost()
									}));

								break;
							}
						}

						ctx.CharacterOfflineTrades.Add(trade);
						ctx.SaveChanges(); // flush
					}
				}
				catch (Exception e)
				{
					LOGGER.Warn(GetType().Name + ": Error while saving offline trader: " + pc.getObjectId() + " " + e,
						e);
				}
			}

			LOGGER.Info(GetType().Name + ": Offline traders stored.");
		}
		catch (Exception e)
		{
			LOGGER.Warn(GetType().Name + ": Error while saving offline traders: " + e, e);
		}
	}

	public void restoreOfflineTraders()
	{
		LOGGER.Info(GetType().Name + ": Loading offline traders...");
		int nTraders = 0;
		try
		{
			using GameServerDbContext ctx = DbFactory.Instance.CreateDbContext();
			var trades = ctx.CharacterOfflineTrades.ToList();
			foreach (var trade in trades)
			{
				DateTime time = trade.Time;
				if (Config.OFFLINE_MAX_DAYS > 0)
				{
					time = time.AddDays(Config.OFFLINE_MAX_DAYS);
					if (time <= DateTime.UtcNow)
					{
						continue; // TODO: filter in query database
					}
				}

				PrivateStoreType typeId = (PrivateStoreType)trade.Type;
				bool isSellBuff = typeId == PrivateStoreType.SELL_BUFFS;

				PrivateStoreType type = isSellBuff ? PrivateStoreType.PACKAGE_SELL : typeId;
				if (!Enum.IsDefined(type))
				{
					LOGGER.Warn(GetType().Name + ": PrivateStoreType with id " + type + " could not be found.");
					continue;
				}

				if (type == PrivateStoreType.NONE)
				{
					continue;
				}

				Player player = null;

				try
				{
					player = Player.load(trade.CharacterId);
					player.setOnlineStatus(true, false);
					player.setOfflineStartTime(time);

					if (isSellBuff)
					{
						player.setSellingBuffs(true);
					}

					player.spawnMe(player.getX(), player.getY(), player.getZ());
					var items = ctx.CharacterOfflineTradeItems.Where(i => i.CharacterId == trade.CharacterId);
					{
						switch (type)
						{
							case PrivateStoreType.BUY:
							{
								foreach (var item in items)
								{
									if (player.getBuyList().addItemByItemId(item.ItemId, item.Count, item.Price) ==
									    null)
									{
										continue;
										// throw new NullPointerException();
									}
								}

								player.getBuyList().setTitle(trade.Title);
								break;
							}
							case PrivateStoreType.SELL:
							case PrivateStoreType.PACKAGE_SELL:
							{
								if (player.isSellingBuffs())
								{
									foreach (var item in items)
									{
										player.getSellingBuffs().Add(new SellBuffHolder(item.ItemId, item.Price));
									}
								}
								else
								{
									foreach (var item in items)
									{
										if (player.getSellList().addItem(item.ItemId, item.Count, item.Price) ==
										    null)
										{
											continue;
											// throw new NullPointerException();
										}
									}
								}

								player.getSellList().setTitle(trade.Title);
								player.getSellList().setPackaged(type == PrivateStoreType.PACKAGE_SELL);
								break;
							}
							case PrivateStoreType.MANUFACTURE:
							{
								foreach (var item in items)
								{
									player.getManufactureItems().put(item.ItemId,
										new ManufactureItem(item.ItemId, item.Price));
								}

								player.setStoreName(trade.Title);
								break;
							}
						}
					}
					player.sitDown();
					if (Config.OFFLINE_SET_NAME_COLOR)
					{
						player.getAppearance().setNameColor(Config.OFFLINE_NAME_COLOR);
					}

					player.setPrivateStoreType(type);
					player.setOnlineStatus(true, true);
					player.restoreEffects();
					if (!Config.OFFLINE_ABNORMAL_EFFECTS.isEmpty())
					{
						player.getEffectList()
							.startAbnormalVisualEffect(
								Config.OFFLINE_ABNORMAL_EFFECTS[Rnd.get(Config.OFFLINE_ABNORMAL_EFFECTS.Length)]);
					}

					player.broadcastUserInfo();
					nTraders++;
				}
				catch (Exception e)
				{
					LOGGER.Warn(GetType().Name + ": Error loading trader: " + player, e);
					if (player != null)
					{
						LeaveWorldPacket leaveWorldPacket = default;
						Disconnection.of(player).defaultSequence(ref leaveWorldPacket);
					}
				}
			}

			World.OFFLINE_TRADE_COUNT = nTraders;
			LOGGER.Info(GetType().Name + ": Loaded " + nTraders + " offline traders.");

			if (!Config.STORE_OFFLINE_TRADE_IN_REALTIME)
			{
				ctx.CharacterOfflineTradeItems.ExecuteDelete();
				ctx.CharacterOfflineTrades.ExecuteDelete();
			}
		}
		catch (Exception e)
		{
			LOGGER.Warn(GetType().Name + ": Error while loading offline traders: ", e);
		}
	}

	[MethodImpl(MethodImplOptions.Synchronized)]
	public void onTransaction(Player trader, bool finished, bool firstCall)
	{
		try
		{
			using GameServerDbContext ctx = DbFactory.Instance.CreateDbContext();
			int traderId = trader.getObjectId();
			String title = null;
			ctx.CharacterOfflineTradeItems.Where(i => i.CharacterId == traderId).ExecuteDelete();

			// Trade is done - clear info
			if (finished)
			{
				ctx.CharacterOfflineTrades.Where(t => t.CharacterId == traderId).ExecuteDelete();
			}
			else
			{
				try
				{
					if ((trader.getClient() == null) || trader.getClient().IsDetached)
					{
						switch (trader.getPrivateStoreType())
						{
							case PrivateStoreType.BUY:
							{
								if (firstCall)
								{
									title = trader.getBuyList().getTitle();
								}

								ctx.CharacterOfflineTradeItems.AddRange(trader.getBuyList().getItems().Select(i =>
									new CharacterOfflineTradeItem
									{
										CharacterId = traderId,
										ItemId = i.getItem().getId(),
										Count = i.getCount(),
										Price = i.getPrice()
									}));

								break;
							}
							case PrivateStoreType.SELL:
							case PrivateStoreType.PACKAGE_SELL:
							{
								if (firstCall)
								{
									title = trader.getSellList().getTitle();
								}

								if (trader.isSellingBuffs())
								{
									ctx.CharacterOfflineTradeItems.AddRange(trader.getSellingBuffs().Select(holder =>
										new CharacterOfflineTradeItem
										{
											CharacterId = traderId,
											ItemId = holder.getSkillId(),
											Count = 0,
											Price = holder.getPrice()
										}));
								}
								else
								{
									ctx.CharacterOfflineTradeItems.AddRange(trader.getSellList().getItems().Select(i =>
										new CharacterOfflineTradeItem
										{
											CharacterId = traderId,
											ItemId = i.getObjectId(),
											Count = i.getCount(),
											Price = i.getPrice()
										}));
								}

								break;
							}
							case PrivateStoreType.MANUFACTURE:
							{
								if (firstCall)
								{
									title = trader.getStoreName();
								}

								ctx.CharacterOfflineTradeItems.AddRange(trader.getManufactureItems().values().Select(
									i =>
										new CharacterOfflineTradeItem
										{
											CharacterId = traderId,
											ItemId = i.getRecipeId(),
											Count = 0,
											Price = i.getCost()
										}));
								break;
							}
						}

						if (firstCall)
						{
							ctx.CharacterOfflineTrades.Add(new()
							{
								CharacterId = traderId,
								Time = trader.getOfflineStartTime() ?? DateTime.UtcNow,
								Type = (byte)(trader.isSellingBuffs()
									? PrivateStoreType.SELL_BUFFS
									: trader.getPrivateStoreType()),
								Title = title
							});
						}
					}

					ctx.SaveChanges();
				}
				catch (Exception e)
				{
					LOGGER.Warn(
						GetType().Name + ": Error while saving offline trader: " + trader.getObjectId() + " " + e, e);
				}
			}
		}
		catch (Exception e)
		{
			LOGGER.Warn(GetType().Name + ": Error while saving offline traders: " + e, e);
		}
	}

	[MethodImpl(MethodImplOptions.Synchronized)]
	public void removeTrader(int traderObjId)
	{
		World.OFFLINE_TRADE_COUNT--;

		try
		{
			using GameServerDbContext ctx = DbFactory.Instance.CreateDbContext();
			ctx.CharacterOfflineTradeItems.Where(i => i.CharacterId == traderObjId).ExecuteDelete();
			ctx.CharacterOfflineTrades.Where(t => t.CharacterId == traderObjId).ExecuteDelete();
		}
		catch (Exception e)
		{
			LOGGER.Warn(GetType().Name + ": Error while removing offline trader: " + traderObjId + " " + e, e);
		}
	}

	/**
	 * Gets the single instance of OfflineTradersTable.
	 * @return single instance of OfflineTradersTable
	 */
	public static OfflineTraderTable getInstance()
	{
		return SingletonHolder.INSTANCE;
	}

	private static class SingletonHolder
	{
		public static readonly OfflineTraderTable INSTANCE = new OfflineTraderTable();
	}
}