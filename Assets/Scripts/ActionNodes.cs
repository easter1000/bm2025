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
    public override NodeState Evaluate(IGameSimulator sim, GamePlayer player)
    {
        float passTendency = 25f;
        passTendency += (player.Rating.passIQ - 85) * 1.5f;
        float overallModifier = (player.Rating.overallAttribute - 85) * 2.5f;
        passTendency -= Mathf.Max(0, overallModifier);

        if (UnityEngine.Random.Range(0, 100) < passTendency) return NodeState.SUCCESS;
        return NodeState.FAILURE;
    }
}

public class Condition_IsOpenFor3 : Node
{
    public override NodeState Evaluate(IGameSimulator sim, GamePlayer player)
    {
        // 3점슛 시도 경향: 3점슛 능력치가 70 이상일 때부터 시도 확률 발생
        float tendency = (player.Rating.threePointShot - 70) * 2.0f;
        if (UnityEngine.Random.Range(0, 100) < tendency)
        {
            sim.AddLog($"{player.Rating.name} is looking for a 3-point opportunity.");
            return NodeState.SUCCESS;
        }
        return NodeState.FAILURE;
    }
}

public class Condition_CanDrive : Node
{
    public override NodeState Evaluate(IGameSimulator sim, GamePlayer player)
    {
        // 돌파 시도 경향: 돌파 관련 능력치에 기반
        float tendency = (player.Rating.drivingDunk + player.Rating.layup - 140) * 1.5f;
         if (UnityEngine.Random.Range(0, 100) < tendency)
        {
            sim.AddLog($"{player.Rating.name} is trying to drive.");
            return NodeState.SUCCESS;
        }
        return NodeState.FAILURE;
    }
}

public class Condition_IsGoodForMidRange : Node
{
    public override NodeState Evaluate(IGameSimulator sim, GamePlayer player)
    {
        // 중거리슛 시도 경향: 중거리슛 능력치에 기반
        float tendency = (player.Rating.midRangeShot - 65) * 2.0f;
        if (UnityEngine.Random.Range(0, 100) < tendency)
        {
            sim.AddLog($"{player.Rating.name} finds space for a mid-range shot.");
            return NodeState.SUCCESS;
        }
        return NodeState.FAILURE;
    }
}
#endregion

#region Actions
public class Action_Try3PointShot : Node
{
    public override NodeState Evaluate(IGameSimulator sim, GamePlayer player)
    {
        player.Stats.FieldGoalsAttempted++;
        player.Stats.ThreePointersAttempted++;

        var defender = sim.GetRandomDefender(player.TeamId);
        var shooterRating = sim.GetAdjustedRating(player);
        var defenderRating = sim.GetAdjustedRating(defender);

        // 1. 블락 확률 계산 (확률 대폭 상향)
        float blockChance = (defenderRating.block - 65) * 0.8f;
        if (UnityEngine.Random.Range(0, 100) < blockChance)
        {
            sim.ResolveBlock(player, defender);
            return NodeState.SUCCESS;
        }

        // 2. 슛 성공률 계산
        float successChance = 40f + (shooterRating.threePointShot - (defenderRating.perimeterDefense * 1.35f)) * 0.8f;
        successChance = Mathf.Clamp(successChance, 5f, 95f);

        sim.AddLog($"{player.Rating.name} shoots a 3-pointer over {defender.Rating.name}. Chance: {successChance:F1}%");

        if (UnityEngine.Random.Range(0, 100) < successChance)
        {
            sim.CurrentState.HomeScore += (player.TeamId == 0) ? 3 : 0;
            sim.CurrentState.AwayScore += (player.TeamId == 1) ? 3 : 0;
            player.Stats.Points += 3;
            player.Stats.FieldGoalsMade++;
            player.Stats.ThreePointersMade++;
            sim.RecordAssist(sim.CurrentState.PotentialAssister);
            sim.UpdatePlusMinusOnScore(player.TeamId, 3);
            sim.AddLog("It's good!");
            string assistText = sim.CurrentState.PotentialAssister != null ? $" (assist by {sim.CurrentState.PotentialAssister.Rating.name})" : "";
            sim.AddUILog($"{player.Rating.name} makes 3-pointer over {defender.Rating.name}{assistText}", player);
            
            sim.CurrentState.PossessingTeamId = 1 - player.TeamId;
            sim.CurrentState.ShotClockSeconds = 24f;
            sim.CurrentState.LastPasser = null;
        }
        else
        {
            sim.AddLog("It's off the mark.");
            sim.AddUILog($"{player.Rating.name} misses 3-pointer over {defender.Rating.name}", player);
            sim.ResolveRebound(player);
            sim.CurrentState.LastPasser = null; 
        }
        sim.ConsumeTime(UnityEngine.Random.Range(4f, 7f)); // 시간 소모 원복
        return NodeState.SUCCESS;
    }
}

public class Action_TryForced3PointShot : Node
{
    public override NodeState Evaluate(IGameSimulator sim, GamePlayer player)
    {
        player.Stats.FieldGoalsAttempted++;
        player.Stats.ThreePointersAttempted++;

        var defender = sim.GetRandomDefender(player.TeamId);
        var shooterRating = sim.GetAdjustedRating(player);
        var defenderRating = sim.GetAdjustedRating(defender);

        // 1. 블락 확률 계산 (압박 상황, 확률 대폭 상향)
        float blockChance = (defenderRating.block - 65) * 0.85f;
        if (UnityEngine.Random.Range(0, 100) < blockChance)
        {
            sim.ResolveBlock(player, defender);
            return NodeState.SUCCESS;
        }

        // 2. 슛 성공률 계산 (기본 성공률은 낮게, 최대 성공률은 50%로 제한)
        float successChance = 20f + (shooterRating.threePointShot - (defenderRating.perimeterDefense * 1.35f)) * 0.8f;
        successChance = Mathf.Clamp(successChance, 5f, 50f);

        sim.AddLog($"FORCED 3-pointer by {player.Rating.name} over {defender.Rating.name}. Chance: {successChance:F1}%");

        if (UnityEngine.Random.Range(0, 100) < successChance)
        {
            sim.CurrentState.HomeScore += (player.TeamId == 0) ? 3 : 0;
            sim.CurrentState.AwayScore += (player.TeamId == 1) ? 3 : 0;
            player.Stats.Points += 3;
            player.Stats.FieldGoalsMade++;
            player.Stats.ThreePointersMade++;
            sim.RecordAssist(sim.CurrentState.PotentialAssister);
            sim.UpdatePlusMinusOnScore(player.TeamId, 3);
            sim.AddLog("It's good!");
            string assistText = sim.CurrentState.PotentialAssister != null ? $" (assist by {sim.CurrentState.PotentialAssister.Rating.name})" : "";
            sim.AddUILog($"FORCED: {player.Rating.name} makes 3-pointer over {defender.Rating.name}{assistText}", player);
            
            sim.CurrentState.PossessingTeamId = 1 - player.TeamId;
            sim.CurrentState.ShotClockSeconds = 24f;
            sim.CurrentState.LastPasser = null;
        }
        else
        {
            sim.AddLog("It's off the mark.");
            sim.AddUILog($"FORCED: {player.Rating.name} misses 3-pointer over {defender.Rating.name}", player);
            sim.ResolveRebound(player);
            sim.CurrentState.LastPasser = null; 
        }
        sim.ConsumeTime(UnityEngine.Random.Range(4f, 7f)); // 시간 소모 원복
        return NodeState.SUCCESS;
    }
}

public class Action_TryMidRangeShot : Node
{
    public override NodeState Evaluate(IGameSimulator sim, GamePlayer player)
    {
        player.Stats.FieldGoalsAttempted++;

        var defender = sim.GetRandomDefender(player.TeamId);
        var shooterRating = sim.GetAdjustedRating(player);
        var defenderRating = sim.GetAdjustedRating(defender);

        // 1. 블락 확률 계산 (확률 대폭 상향)
        float blockChance = (defenderRating.block - 70) * 0.7f;
        if (UnityEngine.Random.Range(0, 100) < blockChance)
        {
            sim.ResolveBlock(player, defender);
            return NodeState.SUCCESS;
        }

        // 2. 슛 성공률 계산
        float successChance = 50f + (shooterRating.midRangeShot - (defenderRating.perimeterDefense * 1.35f)) * 0.85f;
        successChance = Mathf.Clamp(successChance, 15f, 90f);

        sim.AddLog($"{player.Rating.name} takes a mid-range jumper against {defender.Rating.name}. Chance: {successChance:F1}%");
        
        if (UnityEngine.Random.Range(0, 100) < successChance)
        {
            sim.CurrentState.HomeScore += (player.TeamId == 0) ? 2 : 0;
            sim.CurrentState.AwayScore += (player.TeamId == 1) ? 2 : 0;
            player.Stats.Points += 2;
            player.Stats.FieldGoalsMade++;
            sim.RecordAssist(sim.CurrentState.PotentialAssister);
            sim.UpdatePlusMinusOnScore(player.TeamId, 2);
            sim.AddLog("Swish.");
            string assistText = sim.CurrentState.PotentialAssister != null ? $" (assist by {sim.CurrentState.PotentialAssister.Rating.name})" : "";
            sim.AddUILog($"{player.Rating.name} makes mid-range shot over {defender.Rating.name}{assistText}", player);
            
            sim.CurrentState.PossessingTeamId = 1 - player.TeamId;
            sim.CurrentState.ShotClockSeconds = 24f;
            sim.CurrentState.LastPasser = null;
        }
        else
        {
            sim.AddLog("Clanks off the rim.");
            sim.AddUILog($"{player.Rating.name} misses mid-range shot over {defender.Rating.name}", player);
            sim.ResolveRebound(player);
            sim.CurrentState.LastPasser = null;
        }
        sim.ConsumeTime(UnityEngine.Random.Range(4f, 7f)); // 시간 소모 원복
        return NodeState.SUCCESS;
    }
}

public class Action_TryForcedMidRangeShot : Node
{
    public override NodeState Evaluate(IGameSimulator sim, GamePlayer player)
    {
        player.Stats.FieldGoalsAttempted++;
        var defender = sim.GetRandomDefender(player.TeamId);
        var shooterRating = sim.GetAdjustedRating(player);
        var defenderRating = sim.GetAdjustedRating(defender);

        // 1. 블락 확률 계산 (압박 상황, 확률 대폭 상향)
        float blockChance = (defenderRating.block - 70) * 0.75f;
        if (UnityEngine.Random.Range(0, 100) < blockChance)
        {
            sim.ResolveBlock(player, defender);
            return NodeState.SUCCESS;
        }

        // 2. 슛 성공률 계산 (기본 성공률은 낮게, 최대 성공률은 50%로 제한)
        float successChance = 30f + (shooterRating.midRangeShot - (defenderRating.perimeterDefense * 1.35f)) * 0.85f;
        successChance = Mathf.Clamp(successChance, 15f, 50f);

        sim.AddLog($"FORCED mid-range by {player.Rating.name} against {defender.Rating.name}. Chance: {successChance:F1}%");
        
        if (UnityEngine.Random.Range(0, 100) < successChance)
        {
            sim.CurrentState.HomeScore += (player.TeamId == 0) ? 2 : 0;
            sim.CurrentState.AwayScore += (player.TeamId == 1) ? 2 : 0;
            player.Stats.Points += 2;
            player.Stats.FieldGoalsMade++;
            sim.RecordAssist(sim.CurrentState.PotentialAssister);
            sim.UpdatePlusMinusOnScore(player.TeamId, 2);
            sim.AddLog("Swish.");
            string assistText = sim.CurrentState.PotentialAssister != null ? $" (assist by {sim.CurrentState.PotentialAssister.Rating.name})" : "";
            sim.AddUILog($"FORCED: {player.Rating.name} makes mid-range shot over {defender.Rating.name}{assistText}", player);
            
            sim.CurrentState.PossessingTeamId = 1 - player.TeamId;
            sim.CurrentState.ShotClockSeconds = 24f;
            sim.CurrentState.LastPasser = null;
        }
        else
        {
            sim.AddLog("Clanks off the rim.");
            sim.AddUILog($"FORCED: {player.Rating.name} misses mid-range shot over {defender.Rating.name}", player);
            sim.ResolveRebound(player);
            sim.CurrentState.LastPasser = null;
        }
        sim.ConsumeTime(UnityEngine.Random.Range(4f, 7f)); // 시간 소모 원복
        return NodeState.SUCCESS;
    }
}

public class Action_DriveAndFinish : Node
{
    public override NodeState Evaluate(IGameSimulator sim, GamePlayer player)
    {
        var defender = sim.GetRandomDefender(player.TeamId);
        var shooterRating = sim.GetAdjustedRating(player);
        var defenderRating = sim.GetAdjustedRating(defender);
        
        // 1a. 비-스틸 턴오버 확률 (드리블 실수 등)
        float selfTurnoverChance = Mathf.Max(0, 3.0f - (shooterRating.ballHandle - 75) * 0.15f);
        if (UnityEngine.Random.Range(0, 100) < selfTurnoverChance)
        {
            sim.ResolveTurnover(player, null, false); // 수비수 없는 턴오버
            return NodeState.SUCCESS;
        }

        // 1b. 스틸/턴오버 확률 계산 (확률 대폭 상향)
        float turnoverChance = 8.0f + (defenderRating.steal - shooterRating.ballHandle) * 0.4f; 
        if (UnityEngine.Random.Range(0, 100) < turnoverChance)
        {
            sim.ResolveTurnover(player, defender, true); // 스틸에 의한 턴오버
            return NodeState.SUCCESS;
        }

        player.Stats.FieldGoalsAttempted++;
        
        // 2. 파울 확률
        float foulChance = 15f + (shooterRating.drawFoul - 70) * 0.8f;
        if (UnityEngine.Random.Range(0, 100) < foulChance)
        {
            sim.AddUILog($"{defender.Rating.name} commits a shooting foul on {player.Rating.name} ({defender.Stats.PersonalFouls + 1} PF)", defender);
            return sim.ResolveShootingFoul(player, defender, 2);
        }

        // 3. 블락 확률 계산 (확률 대폭 상향)
        float blockChance = (defenderRating.block - 55) * 0.9f;
        if (UnityEngine.Random.Range(0, 100) < blockChance)
        {
            sim.ResolveBlock(player, defender);
            return NodeState.SUCCESS;
        }

        // 4. 슛 성공률 계산
        float offensePower = (shooterRating.layup + shooterRating.drivingDunk) / 2f;
        float successChance = 55f + (offensePower - (defenderRating.interiorDefense * 1.4f)) * 0.9f;
        successChance = Mathf.Clamp(successChance, 10f, 95f);
        
        sim.AddLog($"{player.Rating.name} drives past {defender.Rating.name} for a layup. Chance: {successChance:F1}%");

        if (UnityEngine.Random.Range(0, 100) < successChance)
        {
            sim.CurrentState.HomeScore += (player.TeamId == 0) ? 2 : 0;
            sim.CurrentState.AwayScore += (player.TeamId == 1) ? 2 : 0;
            player.Stats.Points += 2;
            player.Stats.FieldGoalsMade++;
            sim.RecordAssist(sim.CurrentState.PotentialAssister);
            sim.UpdatePlusMinusOnScore(player.TeamId, 2);
            sim.AddLog("Scores!");
            string assistText = sim.CurrentState.PotentialAssister != null ? $" (assist by {sim.CurrentState.PotentialAssister.Rating.name})" : "";
            sim.AddUILog($"{player.Rating.name} makes 2-point shot against {defender.Rating.name}{assistText}", player);

            sim.CurrentState.PossessingTeamId = 1 - player.TeamId;
            sim.CurrentState.ShotClockSeconds = 24f;
            sim.CurrentState.LastPasser = null;
        }
        else
        {
            sim.AddLog("Missed the layup under pressure.");
            sim.AddUILog($"{player.Rating.name} misses 2-point shot against {defender.Rating.name}", player);
            sim.ResolveRebound(player);
            sim.CurrentState.LastPasser = null;
        }
        sim.ConsumeTime(UnityEngine.Random.Range(5f, 8f)); // 시간 소모 원복
        return NodeState.SUCCESS;
    }
}

public class Action_TryForcedDrive : Node
{
    public override NodeState Evaluate(IGameSimulator sim, GamePlayer player)
    {
        var defender = sim.GetRandomDefender(player.TeamId);
        var shooterRating = sim.GetAdjustedRating(player);
        var defenderRating = sim.GetAdjustedRating(defender);

        // 1a. 비-스틸 턴오버 확률 (압박 상황, 확률 증가)
        float selfTurnoverChance = Mathf.Max(0, 4.0f - (shooterRating.ballHandle - 75) * 0.15f);
        if (UnityEngine.Random.Range(0, 100) < selfTurnoverChance)
        {
            sim.ResolveTurnover(player, null, false);
            return NodeState.SUCCESS;
        }

        // 1b. 스틸/턴오버 확률 계산 (압박 상황, 확률 대폭 상향)
        float turnoverChance = 10.0f + (defenderRating.steal - shooterRating.ballHandle) * 0.4f;
        if (UnityEngine.Random.Range(0, 100) < turnoverChance)
        {
            sim.ResolveTurnover(player, defender, true);
            return NodeState.SUCCESS;
        }
        
        player.Stats.FieldGoalsAttempted++;
        
        // 2. 파울 확률
        float foulChance = 18f + (shooterRating.drawFoul - 70) * 0.9f;
        if (UnityEngine.Random.Range(0, 100) < foulChance)
        {
            sim.AddUILog($"FORCED: {defender.Rating.name} commits a shooting foul on {player.Rating.name} ({defender.Stats.PersonalFouls + 1} PF)", defender);
            return sim.ResolveShootingFoul(player, defender, 2);
        }

        // 3. 블락 확률 계산 (압박 상황, 확률 대폭 상향)
        float blockChance = (defenderRating.block - 55) * 1.0f;
        if (UnityEngine.Random.Range(0, 100) < blockChance)
        {
            sim.ResolveBlock(player, defender);
            return NodeState.SUCCESS;
        }

        // 4. 슛 성공률 계산 (기본 성공률은 낮게, 최대 성공률은 50%로 제한)
        float offensePower = (shooterRating.layup + shooterRating.drivingDunk) / 2f;
        float successChance = 35f + (offensePower - (defenderRating.interiorDefense * 1.4f)) * 0.9f;
        successChance = Mathf.Clamp(successChance, 10f, 50f);
        
        sim.AddLog($"FORCED drive by {player.Rating.name} past {defender.Rating.name}. Chance: {successChance:F1}%");

        if (UnityEngine.Random.Range(0, 100) < successChance)
        {
            sim.CurrentState.HomeScore += (player.TeamId == 0) ? 2 : 0;
            sim.CurrentState.AwayScore += (player.TeamId == 1) ? 2 : 0;
            player.Stats.Points += 2;
            player.Stats.FieldGoalsMade++;
            sim.RecordAssist(sim.CurrentState.PotentialAssister);
            sim.UpdatePlusMinusOnScore(player.TeamId, 2);
            sim.AddLog("Scores!");
            string assistText = sim.CurrentState.PotentialAssister != null ? $" (assist by {sim.CurrentState.PotentialAssister.Rating.name})" : "";
            sim.AddUILog($"FORCED: {player.Rating.name} makes 2-point shot against {defender.Rating.name}{assistText}", player);

            sim.CurrentState.PossessingTeamId = 1 - player.TeamId;
            sim.CurrentState.ShotClockSeconds = 24f;
            sim.CurrentState.LastPasser = null;
        }
        else
        {
            sim.AddLog("Missed the layup under pressure.");
            sim.AddUILog($"FORCED: {player.Rating.name} misses 2-point shot against {defender.Rating.name}", player);
            sim.ResolveRebound(player);
            sim.CurrentState.LastPasser = null;
        }
        sim.ConsumeTime(UnityEngine.Random.Range(5f, 8f)); // 시간 소모 원복
        return NodeState.SUCCESS;
    }
}

public class Action_PassToBestTeammate : Node
{
    public override NodeState Evaluate(IGameSimulator sim, GamePlayer player)
    {
        var teammates = sim.GetPlayersOnCourt(player.TeamId).Where(p => p != player).ToList();
        if (teammates.Count == 0)
        {
            sim.ResolveTurnover(player, null, false); // 패스할 곳 없는 턴오버
            return NodeState.FAILURE;
        }
        
        // 1a. 비-스틸 턴오버 확률 (패스 미스 등)
        var passerRating = sim.GetAdjustedRating(player);
        float selfTurnoverChance = Mathf.Max(0, 2.5f - (passerRating.passIQ - 75) * 0.1f);
        if (UnityEngine.Random.Range(0, 100) < selfTurnoverChance)
        {
            sim.ResolveTurnover(player, null, false); // 수비수 없는 턴오버
            return NodeState.SUCCESS;
        }
        
        // 1b. 패스 중 스틸/턴오버 확률 계산 (확률 대폭 상향)
        var defender = sim.GetRandomDefender(player.TeamId);
        var defenderRating = sim.GetAdjustedRating(defender);

        float turnoverChance = 6.0f + (defenderRating.steal - passerRating.passIQ) * 0.35f; 
        if (UnityEngine.Random.Range(0, 100) < turnoverChance)
        {
            sim.ResolveTurnover(player, defender, true); // 스틸에 의한 턴오버
            return NodeState.SUCCESS;
        }
        
        // 2. OVR 기반 가중치 랜덤으로 패스할 동료 선택
        float totalWeight = teammates.Sum(p => Mathf.Pow(p.Rating.overallAttribute, 2.0f));
        float randomPoint = UnityEngine.Random.Range(0, totalWeight);
        
        GamePlayer bestTeammate = null;
        foreach (var teammate in teammates)
        {
            float weight = Mathf.Pow(teammate.Rating.overallAttribute, 2.0f);
            if (randomPoint < weight)
            {
                bestTeammate = teammate;
                break;
            }
            randomPoint -= weight;
        }
        if (bestTeammate == null) bestTeammate = teammates.First();


        sim.CurrentState.PotentialAssister = player;
        sim.CurrentState.LastPasser = bestTeammate;
        sim.AddLog($"{player.Rating.name} passes to {bestTeammate.Rating.name}.");
        
        sim.ConsumeTime(UnityEngine.Random.Range(2f, 5f)); // 패스 시간은 약간 단축된 상태로 유지
        
        return NodeState.SUCCESS;
    }
}

public class Action_ShootFreeThrows : Node
{
    private GamePlayer _shooter;
    private int _attempts;

    public Action_ShootFreeThrows(GamePlayer shooter, int attempts)
    {
        _shooter = shooter;
        _attempts = attempts;
    }
    
    public override NodeState Evaluate(IGameSimulator sim, GamePlayer player)
    {
        int made = 0;
        for (int i = 0; i < _attempts; i++)
        {
            _shooter.Stats.FreeThrowsAttempted++;
            if (UnityEngine.Random.Range(0, 100) < _shooter.Rating.freeThrow)
            {
                made++;
                _shooter.Stats.FreeThrowsMade++;
            }
        }
        
        sim.AddUILog($"{_shooter.Rating.name} makes {made} of {_attempts} free throws", _shooter);
        
        if (made > 0)
        {
            _shooter.Stats.Points += made;
            sim.CurrentState.HomeScore += (_shooter.TeamId == 0) ? made : 0;
            sim.CurrentState.AwayScore += (_shooter.TeamId == 1) ? made : 0;
            sim.UpdatePlusMinusOnScore(_shooter.TeamId, made);
        }
        
        sim.AddLog($"{_shooter.Rating.name} makes {made} of {_attempts} free throws.");
        
        sim.CurrentState.LastPasser = null;
        sim.CurrentState.PotentialAssister = null;
        sim.CurrentState.PossessingTeamId = 1 - _shooter.TeamId;
        sim.CurrentState.ShotClockSeconds = 24f;
        
        return NodeState.SUCCESS;
    }
}

#endregion