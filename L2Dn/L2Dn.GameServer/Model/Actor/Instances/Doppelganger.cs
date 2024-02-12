﻿using System.Text;
using L2Dn.GameServer.AI;
using L2Dn.GameServer.Enums;
using L2Dn.GameServer.Model.Actor.Templates;
using L2Dn.GameServer.Model.Effects;
using L2Dn.GameServer.Model.Olympiads;
using L2Dn.GameServer.Model.Skills;
using L2Dn.GameServer.Network.Enums;
using L2Dn.GameServer.Utilities;
using ThreadPool = System.Threading.ThreadPool;

namespace L2Dn.GameServer.Model.Actor.Instances;

public class Doppelganger : Attackable
{
	private bool _copySummonerEffects = true;
	private ScheduledFuture<?> _attackTask = null;
	private Creature _attackTarget = null;
	
	public Doppelganger(NpcTemplate template, Player owner): base(template)
	{
		setSummoner(owner);
		setCloneObjId(owner.getObjectId());
		setClanId(owner.getClanId());
		setInstance(owner.getInstanceWorld()); // set instance to same as owner
		setXYZInvisible(owner.getX() + Rnd.get(-100, 100), owner.getY() + Rnd.get(-100, 100), owner.getZ());
		((DoppelgangerAI) getAI()).setStartFollowController(true);
		followSummoner(true);
	}
	
	protected override CreatureAI initAI()
	{
		return new DoppelgangerAI(this);
	}
	
	public override void onSpawn()
	{
		base.onSpawn();
		
		if (_copySummonerEffects && (getSummoner() != null))
		{
			foreach (BuffInfo summonerInfo in getSummoner().getEffectList().getEffects())
			{
				if (summonerInfo.getAbnormalTime() > 0)
				{
					BuffInfo info = new BuffInfo(getSummoner(), this, summonerInfo.getSkill(), false, null, null);
					info.setAbnormalTime(summonerInfo.getAbnormalTime());
					getEffectList().add(info);
				}
			}
		}
	}
	
	public void followSummoner(bool followSummoner)
	{
		if (followSummoner)
		{
			if ((getAI().getIntention() == CtrlIntention.AI_INTENTION_IDLE) || (getAI().getIntention() == CtrlIntention.AI_INTENTION_ACTIVE))
			{
				setRunning();
				getAI().setIntention(CtrlIntention.AI_INTENTION_FOLLOW, getSummoner());
			}
		}
		else if (getAI().getIntention() == CtrlIntention.AI_INTENTION_FOLLOW)
		{
			getAI().setIntention(CtrlIntention.AI_INTENTION_IDLE);
		}
	}
	
	public void setCopySummonerEffects(bool copySummonerEffects)
	{
		_copySummonerEffects = copySummonerEffects;
	}
	
	public void stopAttackTask()
	{
		if ((_attackTask != null) && !_attackTask.isCancelled() && !_attackTask.isDone())
		{
			_attackTask.cancel(false);
			_attackTask = null;
			_attackTarget = null;
		}
	}
	
	public void startAttackTask(Creature target)
	{
		stopAttackTask();
		_attackTarget = target;
		_attackTask = ThreadPool.scheduleAtFixedRate(this::thinkCombat, 1000, 1000);
	}
	
	private void thinkCombat()
	{
		if (_attackTarget == null)
		{
			stopAttackTask();
			return;
		}
		
		doAutoAttack(_attackTarget);
		// TODO: Cast skills.
	}
	
	public override byte getPvpFlag()
	{
		return getSummoner() != null ? getSummoner().getPvpFlag() : 0;
	}
	
	public override Team getTeam()
	{
		return getSummoner() != null ? getSummoner().getTeam() : Team.NONE;
	}
	
	public override bool isAutoAttackable(Creature attacker)
	{
		return (getSummoner() != null) ? getSummoner().isAutoAttackable(attacker) : base.isAutoAttackable(attacker);
	}
	
	public override void doAttack(double damage, Creature target, Skill skill, bool isDOT, bool directlyToHp, bool critical, bool reflect)
	{
		base.doAttack(damage, target, skill, isDOT, directlyToHp, critical, reflect);
		sendDamageMessage(target, skill, (int) damage, 0, critical, false, false);
	}
	
	public override void sendDamageMessage(Creature target, Skill skill, int damage, double elementalDamage, bool crit, bool miss, bool elementalCrit)
	{
		if (miss || (getSummoner() == null) || !getSummoner().isPlayer())
		{
			return;
		}
		
		// Prevents the double spam of system messages, if the target is the owning player.
		if (target.getObjectId() != getSummoner().getObjectId())
		{
			if (getActingPlayer().isInOlympiadMode() && (target.isPlayer()) && ((Player) target).isInOlympiadMode() && (((Player) target).getOlympiadGameId() == getActingPlayer().getOlympiadGameId()))
			{
				OlympiadGameManager.getInstance().notifyCompetitorDamage(getSummoner().getActingPlayer(), damage);
			}
			
			 SystemMessage sm;
			if ((target.isHpBlocked() && !target.isNpc()) || (target.isPlayer() && target.isAffected(EffectFlag.DUELIST_FURY) && !getActingPlayer().isAffected(EffectFlag.FACEOFF)))
			{
				sm = new SystemMessage(SystemMessageId.THE_ATTACK_HAS_BEEN_BLOCKED);
			}
			else
			{
				sm = new SystemMessage(SystemMessageId.C1_HAS_DEALT_S3_DAMAGE_TO_C2);
				sm.addNpcName(this);
				sm.addString(target.getName());
				sm.addInt(damage);
				sm.addPopup(target.getObjectId(), getObjectId(), (damage * -1));
			}
			
			sendPacket(sm);
		}
	}
	
	public override void reduceCurrentHp(double damage, Creature attacker, Skill skill)
	{
		base.reduceCurrentHp(damage, attacker, skill);
		
		if ((getSummoner() != null) && getSummoner().isPlayer() && (attacker != null) && !isDead() && !isHpBlocked())
		{
			SystemMessage sm = new SystemMessage(SystemMessageId.C1_HAS_RECEIVED_S3_DAMAGE_FROM_C2);
			sm.addNpcName(this);
			sm.addString(attacker.getName());
			sm.addInt((int) damage);
			sm.addPopup(getObjectId(), attacker.getObjectId(), (int) -damage);
			sendPacket(sm);
		}
	}
	
	public override Player getActingPlayer()
	{
		return getSummoner() != null ? getSummoner().getActingPlayer() : base.getActingPlayer();
	}
	
	public override bool deleteMe()
	{
		stopAttackTask();
		return base.deleteMe();
	}
	
	public override void onTeleported()
	{
		deleteMe(); // In retail, doppelgangers disappear when summoner teleports.
	}
	
	public override void sendPacket<TPacket>(TPacket packet)
	{
		if (getSummoner() != null)
		{
			getSummoner().sendPacket(packet);
		}
	}
	
	public override void sendPacket(SystemMessageId id)
	{
		if (getSummoner() != null)
		{
			getSummoner().sendPacket(id);
		}
	}
	
	public override String ToString()
	{
		StringBuilder sb = new();
		sb.Append(base.ToString());
		sb.Append("(");
		sb.Append(getId());
		sb.Append(") Summoner: ");
		sb.Append(getSummoner());
		return sb.ToString();
	}
}