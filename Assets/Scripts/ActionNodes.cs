using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System;

public abstract class Node
{
    public abstract NodeState Evaluate(IGameSimulator sim, GamePlayer player);
}

public enum NodeState { SUCCESS, FAILURE }

public class Sequence : Node
{
    private List<Node> _nodes;
    public Sequence(List<Node> nodes) { _nodes = nodes; }
    public override NodeState Evaluate(IGameSimulator sim, GamePlayer player)
    {
        foreach (var node in _nodes)
        {
            if (node.Evaluate(sim, player) == NodeState.FAILURE) return NodeState.FAILURE;
        }
        return NodeState.SUCCESS;
    }
}

public class Selector : Node
{
    private List<Node> _nodes;
    public Selector(List<Node> nodes) { _nodes = nodes; }
    public override NodeState Evaluate(IGameSimulator sim, GamePlayer player)
    {
        foreach (var node in _nodes)
        {
            if (node.Evaluate(sim, player) == NodeState.SUCCESS) return NodeState.SUCCESS;
        }
        return NodeState.FAILURE;
    }
}


#region Conditions

public class Condition_IsShotClockLow : Node
{
    public override NodeState Evaluate(IGameSimulator sim, GamePlayer player)
    {
        return sim.CurrentState.ShotClockSeconds < 5f ? NodeState.SUCCESS : NodeState.FAILURE;
    }
}

public class Condition_IsGoodPassOpportunity : Node
{
    private System.Random _random;
    public Condition_IsGoodPassOpportunity(System.Random random) { _random = random; }

    public override NodeState Evaluate(IGameSimulator sim, GamePlayer player)
    {
        float passTendency = 55f; // 기본 패스 확률 조정 (65f -> 55f)
        passTendency += (player.Rating.passIQ - 80) * 1.5f; // passIQ 영향력 강화

        return (_random.NextDouble() * 100) < passTendency ? NodeState.SUCCESS : NodeState.FAILURE;
    }
}

public class Condition_IsOpenFor3 : Node
{
    private System.Random _random;
    public Condition_IsOpenFor3(System.Random random) { _random = random; }
    public override NodeState Evaluate(IGameSimulator sim, GamePlayer player)
    {
        float tendency = 15 + (player.Rating.threePointShot - 70) * 2.0f;
        return (_random.NextDouble() * 100) < tendency ? NodeState.SUCCESS : NodeState.FAILURE;
    }
}

public class Condition_CanDrive : Node
{
    private System.Random _random;
    public Condition_CanDrive(System.Random random) { _random = random; }
    public override NodeState Evaluate(IGameSimulator sim, GamePlayer player)
    {
        float tendency = 15 + (player.Rating.drivingDunk + player.Rating.layup - 140) * 1.5f;
        return (_random.NextDouble() * 100) < tendency ? NodeState.SUCCESS : NodeState.FAILURE;
    }
}

public class Condition_IsGoodForMidRange : Node
{
    private System.Random _random;
    public Condition_IsGoodForMidRange(System.Random random) { _random = random; }
    public override NodeState Evaluate(IGameSimulator sim, GamePlayer player)
    {
        float tendency = 12 + (player.Rating.midRangeShot - 65) * 2.0f;
        return (_random.NextDouble() * 100) < tendency ? NodeState.SUCCESS : NodeState.FAILURE;
    }
}

public class Condition_IsGood3PointShooter : Node
{
    private int _threshold;
    public Condition_IsGood3PointShooter(int threshold) { _threshold = threshold; }

    public override NodeState Evaluate(IGameSimulator sim, GamePlayer player)
    {
        // 선수의 능력치가 스태미나 등에 따라 조정될 수 있으므로, 원본 능력치인 Rating을 사용합니다.
        return player.Rating.threePointShot > _threshold ? NodeState.SUCCESS : NodeState.FAILURE;
    }
}
#endregion

#region Actions
public class Action_Try3PointShot : Node
{
    private System.Random _random;
    public Action_Try3PointShot(System.Random random) { _random = random; }

    public override NodeState Evaluate(IGameSimulator game, GamePlayer player)
    {
        var adjustedRating = game.GetAdjustedRating(player);
        var defender = game.GetRandomDefender(player.TeamId);
        var adjustedDefender = game.GetAdjustedRating(defender);

        float successChance = 40 + (adjustedRating.threePointShot * 0.5f);
        if (defender != null)
        {
            successChance -= adjustedDefender.perimeterDefense * 0.4f;
            successChance -= adjustedDefender.speed * 0.1f;
        }

        bool madeShot = (_random.NextDouble() * 100) < successChance;
        
        game.ConsumeTime((float)(_random.NextDouble() * (5 - 2) + 2));

        player.Stats.FieldGoalsAttempted++;
        player.Stats.ThreePointersAttempted++;

        if (madeShot)
        {
            player.Stats.FieldGoalsMade++;
            player.Stats.ThreePointersMade++;
            player.Stats.Points += 3;
            if(player.TeamId == 0) game.CurrentState.HomeScore += 3; else game.CurrentState.AwayScore += 3;
            game.AddUILog($"{player.Rating.name} makes a 3-point shot!", player);
            
            game.RecordAssist(game.CurrentState.PotentialAssister);
            game.UpdatePlusMinusOnScore(player.TeamId, 3);
            
            game.CurrentState.PossessingTeamId = 1 - player.TeamId;
            game.CurrentState.LastPasser = null;
            game.CurrentState.ShotClockSeconds = 24f;
        }
        else
        {
            game.AddUILog($"{player.Rating.name} misses a 3-point shot.", player);
            game.ResolveRebound(player);
        }
        game.CurrentState.PotentialAssister = null;
        return NodeState.SUCCESS;
    }
}


public class Action_TryForced3PointShot : Node
{
    private System.Random _random;
    public Action_TryForced3PointShot(System.Random random) { _random = random; }
    public override NodeState Evaluate(IGameSimulator game, GamePlayer player)
    {
        var adjustedRating = game.GetAdjustedRating(player);
        var defender = game.GetRandomDefender(player.TeamId);
        var adjustedDefender = game.GetAdjustedRating(defender);
        
        float successChance = 25 + adjustedRating.threePointShot * 0.4f;
        if(defender != null) successChance -= adjustedDefender.perimeterDefense * 0.4f;

        bool madeShot = (_random.NextDouble() * 100) < successChance;
        game.ConsumeTime((float)(_random.NextDouble() * (4-2)+2));

        player.Stats.FieldGoalsAttempted++;
        player.Stats.ThreePointersAttempted++;

        if (madeShot)
        {
            player.Stats.FieldGoalsMade++;
            player.Stats.ThreePointersMade++;
            player.Stats.Points += 3;
            if(player.TeamId == 0) game.CurrentState.HomeScore += 3; else game.CurrentState.AwayScore += 3;
            game.AddUILog($"{player.Rating.name} makes a forced 3-point shot!", player);
            
            game.RecordAssist(game.CurrentState.PotentialAssister);
            game.UpdatePlusMinusOnScore(player.TeamId, 3);

            game.CurrentState.PossessingTeamId = 1 - player.TeamId;
            game.CurrentState.ShotClockSeconds = 24f;
            game.CurrentState.LastPasser = null;
        }
        else
        {
            game.AddUILog($"{player.Rating.name} misses a forced 3-point shot.", player);
            game.ResolveRebound(player);
        }
        game.CurrentState.PotentialAssister = null;
        return NodeState.SUCCESS;
    }
}


public class Action_TryMidRangeShot : Node
{
    private System.Random _random;
    public Action_TryMidRangeShot(System.Random random) { _random = random; }
    public override NodeState Evaluate(IGameSimulator game, GamePlayer player)
    {
        var adjustedRating = game.GetAdjustedRating(player);
        var defender = game.GetRandomDefender(player.TeamId);
        var adjustedDefender = game.GetAdjustedRating(defender);
        
        float successChance = 50 + adjustedRating.midRangeShot * 0.5f;
        if(defender != null) successChance -= (adjustedDefender.perimeterDefense + adjustedDefender.interiorDefense) * 0.35f;

        bool madeShot = (_random.NextDouble() * 100) < successChance;
        game.ConsumeTime((float)(_random.NextDouble() * (6-3)+3));

        player.Stats.FieldGoalsAttempted++;

        if (madeShot)
        {
            player.Stats.FieldGoalsMade++;
            player.Stats.Points += 2;
            if(player.TeamId == 0) game.CurrentState.HomeScore += 2; else game.CurrentState.AwayScore += 2;
            game.AddUILog($"{player.Rating.name} makes a mid-range shot!", player);

            game.RecordAssist(game.CurrentState.PotentialAssister);
            game.UpdatePlusMinusOnScore(player.TeamId, 2);

            game.CurrentState.PossessingTeamId = 1 - player.TeamId;
            game.CurrentState.ShotClockSeconds = 24f;
            game.CurrentState.LastPasser = null;
        }
        else
        {
            game.AddUILog($"{player.Rating.name} misses a mid-range shot.", player);
            game.ResolveRebound(player);
        }
        game.CurrentState.PotentialAssister = null;
        return NodeState.SUCCESS;
    }
}

public class Action_TryForcedMidRangeShot : Node
{
    private System.Random _random;
    public Action_TryForcedMidRangeShot(System.Random random) { _random = random; }
    public override NodeState Evaluate(IGameSimulator game, GamePlayer player)
    {
        var adjustedRating = game.GetAdjustedRating(player);
        var defender = game.GetRandomDefender(player.TeamId);
        var adjustedDefender = game.GetAdjustedRating(defender);

        float successChance = 35 + adjustedRating.midRangeShot * 0.4f;
        if(defender != null) successChance -= (adjustedDefender.perimeterDefense + adjustedDefender.interiorDefense) * 0.3f;
        
        bool madeShot = (_random.NextDouble() * 100) < successChance;
        game.ConsumeTime((float)(_random.NextDouble() * (5-2)+2));

        player.Stats.FieldGoalsAttempted++;

        if (madeShot)
        {
            player.Stats.FieldGoalsMade++;
            player.Stats.Points += 2;
            if(player.TeamId == 0) game.CurrentState.HomeScore += 2; else game.CurrentState.AwayScore += 2;
            game.AddUILog($"{player.Rating.name} makes a forced mid-range shot!", player);

            game.RecordAssist(game.CurrentState.PotentialAssister);
            game.UpdatePlusMinusOnScore(player.TeamId, 2);

            game.CurrentState.PossessingTeamId = 1 - player.TeamId;
            game.CurrentState.ShotClockSeconds = 24f;
            game.CurrentState.LastPasser = null;
        }
        else
        {
            game.AddUILog($"{player.Rating.name} misses a forced mid-range shot.", player);
            game.ResolveRebound(player);
        }
        game.CurrentState.PotentialAssister = null;
        return NodeState.SUCCESS;
    }
}


public class Action_DriveAndFinish : Node
{
    private System.Random _random;
    public Action_DriveAndFinish(System.Random random) { _random = random; }
    public override NodeState Evaluate(IGameSimulator game, GamePlayer player)
    {
        var adjustedRating = game.GetAdjustedRating(player);
        var defender = game.GetRandomDefender(player.TeamId);
        var adjustedDefender = game.GetAdjustedRating(defender);

        float foulChance = adjustedRating.drawFoul * 0.4f; // 파울 유도 능력 가중치 상향
        
        if ((_random.NextDouble() * 100) < foulChance && defender != null)
        {
            return game.ResolveShootingFoul(player, defender, 2);
        }

        float successChance = 55 + adjustedRating.layup * 0.5f + adjustedRating.drivingDunk * 0.1f;
        if(defender != null) successChance -= adjustedDefender.interiorDefense * 0.4f;

        bool madeShot = (_random.NextDouble() * 100) < successChance;
        game.ConsumeTime((float)(_random.NextDouble() * (8-4)+4));

        player.Stats.FieldGoalsAttempted++;

        if (madeShot)
        {
            player.Stats.FieldGoalsMade++;
            player.Stats.Points += 2;
            if(player.TeamId == 0) game.CurrentState.HomeScore += 2; else game.CurrentState.AwayScore += 2;
            game.AddUILog($"{player.Rating.name} drives and finishes at the rim!", player);

            game.RecordAssist(game.CurrentState.PotentialAssister);
            game.UpdatePlusMinusOnScore(player.TeamId, 2);

            game.CurrentState.PossessingTeamId = 1 - player.TeamId;
            game.CurrentState.ShotClockSeconds = 24f;
            game.CurrentState.LastPasser = null;
        }
        else
        {
            if (defender != null && (_random.NextDouble() * 100) < (adjustedDefender.block * 0.2f))
            {
                game.ResolveBlock(player, defender);
            }
            else
            {
                game.AddUILog($"{player.Rating.name} misses the layup.", player);
                game.ResolveRebound(player);
            }
        }
        game.CurrentState.PotentialAssister = null;
        return NodeState.SUCCESS;
    }
}


public class Action_TryForcedDrive : Node
{
    private System.Random _random;
    public Action_TryForcedDrive(System.Random random) { _random = random; }
    public override NodeState Evaluate(IGameSimulator game, GamePlayer player)
    {
        var adjustedRating = game.GetAdjustedRating(player);
        var defender = game.GetRandomDefender(player.TeamId);
        var adjustedDefender = game.GetAdjustedRating(defender);

        float foulChance = adjustedRating.drawFoul * 0.3f; // 파울 유도 능력 가중치 상향
        
        if ((_random.NextDouble() * 100) < foulChance && defender != null)
        {
            return game.ResolveShootingFoul(player, defender, 2);
        }

        float successChance = 40 + adjustedRating.layup * 0.4f + adjustedRating.drivingDunk * 0.05f;
        if(defender != null) successChance -= adjustedDefender.interiorDefense * 0.4f;

        bool madeShot = (_random.NextDouble() * 100) < successChance;
        game.ConsumeTime((float)(_random.NextDouble() * (7-3)+3));

        player.Stats.FieldGoalsAttempted++;

        if (madeShot)
        {
            player.Stats.FieldGoalsMade++;
            player.Stats.Points += 2;
            if(player.TeamId == 0) game.CurrentState.HomeScore += 2; else game.CurrentState.AwayScore += 2;
            game.AddUILog($"{player.Rating.name} with a tough finish inside!", player);

            game.RecordAssist(game.CurrentState.PotentialAssister);
            game.UpdatePlusMinusOnScore(player.TeamId, 2);

            game.CurrentState.PossessingTeamId = 1 - player.TeamId;
            game.CurrentState.ShotClockSeconds = 24f;
            game.CurrentState.LastPasser = null;
        }
        else
        {
            if (defender != null && (_random.NextDouble() * 100) < adjustedDefender.block * 0.15f)
            {
                game.ResolveBlock(player, defender);
            }
            else
            {
                game.AddUILog($"{player.Rating.name} misses a forced shot inside.", player);
                game.ResolveRebound(player);
            }
        }
        game.CurrentState.PotentialAssister = null;
        return NodeState.SUCCESS;
    }
}


public class Action_PassToBestTeammate : Node
{
    private System.Random _random;
    public Action_PassToBestTeammate(System.Random random) { _random = random; }
    public override NodeState Evaluate(IGameSimulator game, GamePlayer player)
    {
        var teammates = game.GetPlayersOnCourt(player.TeamId).Where(p => p.Rating.player_id != player.Rating.player_id).ToList();
        if (teammates.Count == 0) return NodeState.FAILURE; // 코트에 혼자일 경우 실패 반환

        GamePlayer bestTarget = null;
        float bestScore = -1f;

        foreach (var p in teammates)
        {
            float score = p.EffectiveOverall * 0.7f + (_random.Next(0, 31)); // OVR 70%, 랜덤 30%
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = p;
            }
        }

        if (bestTarget == null) return NodeState.FAILURE;

        game.ConsumeTime((float)(_random.NextDouble() * (5-1)+1));

        var passerAdjusted = game.GetAdjustedRating(player);
        var defender = game.GetRandomDefender(player.TeamId);
        
        float turnoverChance = 5f;
        if(defender != null)
        {
            var defAdjusted = game.GetAdjustedRating(defender);
            turnoverChance += defAdjusted.steal * 0.1f;
        }
        turnoverChance -= passerAdjusted.passIQ * 0.1f;
        turnoverChance -= passerAdjusted.ballHandle * 0.05f;

        if ((_random.NextDouble() * 100) < turnoverChance)
        {
            game.AddUILog($"{player.Rating.name}'s pass is stolen!", player);
            game.ResolveTurnover(player, defender, true);
        }
        else
        {
            game.CurrentState.LastPasser = bestTarget;
            game.CurrentState.PotentialAssister = player;
            game.AddUILog($"{player.Rating.name} passes to {bestTarget.Rating.name}.", player);
        }
        return NodeState.SUCCESS;
    }
}

/// <summary>
/// 어떤 공격 옵션도 선택할 수 없을 때 강제로 턴오버를 발생시키는 최후의 수단 노드.
/// </summary>
public class Action_ForceTurnover : Node
{
    public override NodeState Evaluate(IGameSimulator sim, GamePlayer player)
    {
        sim.AddUILog($"{player.Rating.name} couldn't find an option and commits a turnover.", player);
        sim.ResolveTurnover(player, null, false);
        return NodeState.SUCCESS;
    }
}

public class Action_ShootFreeThrows : Node
{
    private GamePlayer _shooter;
    private int _attempts;
    private System.Random _random;

    public Action_ShootFreeThrows(GamePlayer shooter, int attempts, System.Random random)
    {
        _shooter = shooter;
        _attempts = attempts;
        _random = random;
    }

    public override NodeState Evaluate(IGameSimulator game, GamePlayer player)
    {
        int madeShots = 0;
        var adjustedRating = game.GetAdjustedRating(_shooter);

        for (int i = 0; i < _attempts; i++)
        {
            game.ConsumeTime((float)(_random.NextDouble() * (4-2)+2));
            _shooter.Stats.FreeThrowsAttempted++;
            if ((_random.NextDouble() * 100) < adjustedRating.freeThrow)
            {
                madeShots++;
                _shooter.Stats.FreeThrowsMade++;
            }
        }

        if (madeShots > 0)
        {
            _shooter.Stats.Points += madeShots;
            if(_shooter.TeamId == 0) game.CurrentState.HomeScore += madeShots; else game.CurrentState.AwayScore += madeShots;
            game.UpdatePlusMinusOnScore(_shooter.TeamId, madeShots);
        }
        
        game.AddUILog($"{_shooter.Rating.name} makes {madeShots} of {_attempts} free throws.", _shooter);

        game.CurrentState.PossessingTeamId = 1 - _shooter.TeamId;
        game.CurrentState.ShotClockSeconds = 24f;
        game.CurrentState.LastPasser = null;
        game.CurrentState.PotentialAssister = null;
        
        return NodeState.SUCCESS;
    }
}

#endregion