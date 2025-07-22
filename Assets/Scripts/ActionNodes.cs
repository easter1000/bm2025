using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class Node
{
    public abstract NodeState Evaluate(GameSimulator sim, GameSimulator.GamePlayer player);
}

public enum NodeState { SUCCESS, FAILURE }

public class Sequence : Node
{
    private List<Node> _nodes;
    public Sequence(List<Node> nodes) { _nodes = nodes; }
    public override NodeState Evaluate(GameSimulator sim, GameSimulator.GamePlayer player)
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
    public override NodeState Evaluate(GameSimulator sim, GameSimulator.GamePlayer player)
    {
        foreach (var node in _nodes)
        {
            if (node.Evaluate(sim, player) == NodeState.SUCCESS) return NodeState.SUCCESS;
        }
        return NodeState.FAILURE;
    }
}

// [신규] 공격 시간이 얼마 남지 않았는지 확인하는 조건
public class Condition_IsShotClockLow : Node
{
    public override NodeState Evaluate(GameSimulator sim, GameSimulator.GamePlayer player)
    {
        if (sim.CurrentState.ShotClockSeconds < 5f) // 압박 시간 7초 -> 5초로 변경
        {
            sim.AddLog("Shot clock is winding down! Gotta shoot!");
            return NodeState.SUCCESS;
        }
        return NodeState.FAILURE;
    }
}

// --- 조건 노드: 결정적인 패스 기회가 있는가? ---
public class Condition_IsGoodPassOpportunity : Node
{
    public override NodeState Evaluate(GameSimulator sim, GameSimulator.GamePlayer player)
    {
        // 패스 빈도 현실화: 기본 확률을 45%로 유지
        float passTendency = 45f; 
        passTendency += (player.Rating.passIQ - 80); 
        if (UnityEngine.Random.Range(0, 100) < passTendency) return NodeState.SUCCESS;
        return NodeState.FAILURE;
    }
}

public class Condition_IsOpenFor3 : Node
{
    public override NodeState Evaluate(GameSimulator sim, GameSimulator.GamePlayer player)
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
    public override NodeState Evaluate(GameSimulator sim, GameSimulator.GamePlayer player)
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
    public override NodeState Evaluate(GameSimulator sim, GameSimulator.GamePlayer player)
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

// --- 액션 노드 ---
public class Action_Try3PointShot : Node
{
    public override NodeState Evaluate(GameSimulator sim, GameSimulator.GamePlayer player)
    {
        player.Stats.FieldGoalsAttempted++;
        player.Stats.ThreePointersAttempted++;

        var defender = sim.GetRandomDefender(player.TeamId);
        var shooterRating = sim.GetAdjustedRating(player);
        var defenderRating = sim.GetAdjustedRating(defender);

        // 성공률: 수비수 스탯에 1.2배 가중치를 부여하여 공수 밸런스 조정
        float successChance = 40f + (shooterRating.threePointShot - (defenderRating.perimeterDefense * 1.2f)) * 0.8f;
        successChance = UnityEngine.Mathf.Clamp(successChance, 5f, 95f);

        sim.AddLog($"{player.Rating.name} shoots a 3-pointer over {defender.Rating.name}. Chance: {successChance:F1}%");

        if (UnityEngine.Random.Range(0, 100) < successChance)
        {
            // (득점 및 스탯 기록 로직은 이전과 동일)
            sim.CurrentState.HomeScore += (player.TeamId == 0) ? 3 : 0;
            sim.CurrentState.AwayScore += (player.TeamId == 1) ? 3 : 0;
            player.Stats.Points += 3;
            player.Stats.FieldGoalsMade++;
            player.Stats.ThreePointersMade++;
            sim.RecordAssist(sim.CurrentState.LastPasser);
            sim.UpdatePlusMinusOnScore(player.TeamId, 3);
            sim.AddLog("It's good!");
            
            // 공격권 전환 및 리셋
            sim.CurrentState.PossessingTeamId = 1 - player.TeamId;
            sim.CurrentState.ShotClockSeconds = 24f;
            sim.CurrentState.CurrentBallHandler = null;
            sim.CurrentState.LastPasser = null;
        }
        else
        {
            sim.AddLog("It's off the mark.");
            sim.ResolveRebound(player);
            sim.CurrentState.CurrentBallHandler = null; // 리바운드 상황이므로 볼 핸들러 초기화
        }
        sim.ConsumeTime(UnityEngine.Random.Range(4f, 8f)); // 공격 시간 소모 3-6초 -> 4-8초로 증가
        return NodeState.SUCCESS;
    }
}

public class Action_DriveAndFinish : Node
{
    public override NodeState Evaluate(GameSimulator sim, GameSimulator.GamePlayer player)
    {
        player.Stats.FieldGoalsAttempted++;

        var defender = sim.GetRandomDefender(player.TeamId);
        var shooterRating = sim.GetAdjustedRating(player);
        var defenderRating = sim.GetAdjustedRating(defender);

        // 파울 확률
        float foulChance = (defenderRating.drawFoul - 50) * 0.5f;
        if (UnityEngine.Random.Range(0, 100) < foulChance)
        {
            return sim.ResolveShootingFoul(player, defender, 2);
        }

        // 성공률: 내부 수비수 스탯에 1.25배 가중치를 부여하여 공수 밸런스 조정
        float offensePower = (shooterRating.layup + shooterRating.drivingDunk) / 2f;
        float successChance = 55f + (offensePower - (defenderRating.interiorDefense * 1.25f)) * 0.9f;
        successChance = UnityEngine.Mathf.Clamp(successChance, 10f, 95f);
        
        sim.AddLog($"{player.Rating.name} drives past {defender.Rating.name} for a layup. Chance: {successChance:F1}%");

        if (UnityEngine.Random.Range(0, 100) < successChance)
        {
            // (득점 및 스탯 기록 로직은 이전과 동일)
            sim.CurrentState.HomeScore += (player.TeamId == 0) ? 2 : 0;
            sim.CurrentState.AwayScore += (player.TeamId == 1) ? 2 : 0;
            player.Stats.Points += 2;
            player.Stats.FieldGoalsMade++;
            sim.RecordAssist(sim.CurrentState.LastPasser);
            sim.UpdatePlusMinusOnScore(player.TeamId, 2);
            sim.AddLog("Scores!");

            // 공격권 전환 및 리셋
            sim.CurrentState.PossessingTeamId = 1 - player.TeamId;
            sim.CurrentState.ShotClockSeconds = 24f;
            sim.CurrentState.CurrentBallHandler = null;
            sim.CurrentState.LastPasser = null;
        }
        else
        {
            sim.AddLog("Missed the layup under pressure.");
            sim.ResolveRebound(player);
            sim.CurrentState.CurrentBallHandler = null;
        }
        sim.ConsumeTime(UnityEngine.Random.Range(5f, 9f)); // 공격 시간 소모 4-7초 -> 5-9초로 증가
        return NodeState.SUCCESS;
    }
}

public class Action_TryMidRangeShot : Node
{
    public override NodeState Evaluate(GameSimulator sim, GameSimulator.GamePlayer player)
    {
        player.Stats.FieldGoalsAttempted++;

        var defender = sim.GetRandomDefender(player.TeamId);
        var shooterRating = sim.GetAdjustedRating(player);
        var defenderRating = sim.GetAdjustedRating(defender);

        // 성공률: 수비수 스탯에 1.2배 가중치를 부여하여 공수 밸런스 조정
        float successChance = 50f + (shooterRating.midRangeShot - (defenderRating.perimeterDefense * 1.2f)) * 0.85f;
        successChance = UnityEngine.Mathf.Clamp(successChance, 15f, 90f);

        sim.AddLog($"{player.Rating.name} takes a mid-range jumper against {defender.Rating.name}. Chance: {successChance:F1}%");
        
        if (UnityEngine.Random.Range(0, 100) < successChance)
        {
            // (득점 및 스탯 기록 로직은 이전과 동일)
            sim.CurrentState.HomeScore += (player.TeamId == 0) ? 2 : 0;
            sim.CurrentState.AwayScore += (player.TeamId == 1) ? 2 : 0;
            player.Stats.Points += 2;
            player.Stats.FieldGoalsMade++;
            sim.RecordAssist(sim.CurrentState.LastPasser);
            sim.UpdatePlusMinusOnScore(player.TeamId, 2);
            sim.AddLog("Swish.");
            
            // 공격권 전환 및 리셋
            sim.CurrentState.PossessingTeamId = 1 - player.TeamId;
            sim.CurrentState.ShotClockSeconds = 24f;
            sim.CurrentState.CurrentBallHandler = null;
            sim.CurrentState.LastPasser = null;
        }
        else
        {
            sim.AddLog("Clanks off the rim.");
            sim.ResolveRebound(player);
            sim.CurrentState.CurrentBallHandler = null;
        }
        sim.ConsumeTime(UnityEngine.Random.Range(4f, 8f)); // 공격 시간 소모 3-6초 -> 4-8초로 증가
        return NodeState.SUCCESS;
    }
}

public class Action_ShootFreeThrows : Node
{
    private GameSimulator.GamePlayer _shooter;
    private int _attempts;

    public Action_ShootFreeThrows(GameSimulator.GamePlayer shooter, int attempts)
    {
        _shooter = shooter;
        _attempts = attempts;
    }

    public override NodeState Evaluate(GameSimulator sim, GameSimulator.GamePlayer player)
    {
        int made = 0;
        for (int i = 0; i < _attempts; i++)
        {
            _shooter.Stats.FreeThrowsAttempted++;
            if (Random.Range(0, 100) < _shooter.Rating.freeThrow)
            {
                made++;
                _shooter.Stats.FreeThrowsMade++;
            }
        }
        
        if (made > 0)
        {
            _shooter.Stats.Points += made;
            sim.CurrentState.HomeScore += (_shooter.TeamId == 0) ? made : 0;
            sim.CurrentState.AwayScore += (_shooter.TeamId == 1) ? made : 0;
            sim.UpdatePlusMinusOnScore(_shooter.TeamId, made);
        }
        
        sim.AddLog($"{_shooter.Rating.name} makes {made} of {_attempts} free throws.");
        
        // 자유투 후에는 항상 공격권이 전환되므로 볼 핸들러 초기화 및 리셋
        sim.CurrentState.CurrentBallHandler = null;
        sim.CurrentState.LastPasser = null;
        sim.CurrentState.PossessingTeamId = 1 - _shooter.TeamId;
        sim.CurrentState.ShotClockSeconds = 24f;
        
        return NodeState.SUCCESS;
    }
}

public class Action_PassToBestTeammate : Node
{
    public override NodeState Evaluate(GameSimulator sim, GameSimulator.GamePlayer player)
    {
        var teammates = sim.GetPlayersOnCourt(player.TeamId).Where(p => p != player).ToList();
        if (teammates.Count == 0)
        {
            player.Stats.Turnovers++;
            sim.AddLog($"{player.Rating.name} has no one to pass to, turnover!");
            sim.CurrentState.PossessingTeamId = 1 - player.TeamId;
            sim.CurrentState.ShotClockSeconds = 24f;
            sim.CurrentState.CurrentBallHandler = null; // 턴오버 시 볼 핸들러 초기화
            return NodeState.FAILURE;
        }

        var bestTeammate = teammates.OrderByDescending(p => p.Rating.overallAttribute).First();
        sim.CurrentState.LastPasser = player; // 어시스트 추적
        sim.CurrentState.CurrentBallHandler = bestTeammate; // 공 받은 선수로 교체
        sim.AddLog($"{player.Rating.name} passes to {bestTeammate.Rating.name}.");
        
        // 공격 템포 조절: 패스 시 3~6초의 시간을 소모하도록 수정
        sim.ConsumeTime(UnityEngine.Random.Range(3f, 6f));
        
        return NodeState.SUCCESS;
    }
}

// --- [신규] 샷클락 압박 상황에서 사용하는 성공률 낮은 버전의 액션 노드들 ---

public class Action_TryForced3PointShot : Node
{
    public override NodeState Evaluate(GameSimulator sim, GameSimulator.GamePlayer player)
    {
        player.Stats.FieldGoalsAttempted++;
        player.Stats.ThreePointersAttempted++;

        var defender = sim.GetRandomDefender(player.TeamId);
        var shooterRating = sim.GetAdjustedRating(player);
        var defenderRating = sim.GetAdjustedRating(defender);

        // 성공률: 기존 로직에서 10% 페널티 + 수비 가중치(1.2배) 적용
        float successChance = 30f + (shooterRating.threePointShot - (defenderRating.perimeterDefense * 1.2f)) * 0.8f;
        successChance = UnityEngine.Mathf.Clamp(successChance, 5f, 85f);

        sim.AddLog($"FORCED 3-pointer by {player.Rating.name} over {defender.Rating.name}. Chance: {successChance:F1}%");

        if (UnityEngine.Random.Range(0, 100) < successChance)
        {
            sim.CurrentState.HomeScore += (player.TeamId == 0) ? 3 : 0;
            sim.CurrentState.AwayScore += (player.TeamId == 1) ? 3 : 0;
            player.Stats.Points += 3;
            player.Stats.FieldGoalsMade++;
            player.Stats.ThreePointersMade++;
            sim.RecordAssist(sim.CurrentState.LastPasser);
            sim.UpdatePlusMinusOnScore(player.TeamId, 3);
            sim.AddLog("It's good!");
            
            // 공격권 전환 및 리셋
            sim.CurrentState.PossessingTeamId = 1 - player.TeamId;
            sim.CurrentState.ShotClockSeconds = 24f;
            sim.CurrentState.CurrentBallHandler = null;
            sim.CurrentState.LastPasser = null;
        }
        else
        {
            sim.AddLog("It's off the mark.");
            sim.ResolveRebound(player);
            sim.CurrentState.CurrentBallHandler = null; // 리바운드 상황이므로 볼 핸들러 초기화
        }
        sim.ConsumeTime(UnityEngine.Random.Range(4f, 8f)); // 공격 시간 소모 3-6초 -> 4-8초로 증가
        return NodeState.SUCCESS;
    }
}

public class Action_TryForcedDrive : Node
{
    public override NodeState Evaluate(GameSimulator sim, GameSimulator.GamePlayer player)
    {
        player.Stats.FieldGoalsAttempted++;
        var defender = sim.GetRandomDefender(player.TeamId);
        
        var shooterRating = sim.GetAdjustedRating(player);
        var defenderRating = sim.GetAdjustedRating(defender);

        // 파울 확률
        float foulChance = (defenderRating.drawFoul - 50) * 0.5f;
        if (UnityEngine.Random.Range(0, 100) < foulChance)
        {
            return sim.ResolveShootingFoul(player, defender, 2);
        }

        // 성공률: 기존 로직에서 10% 페널티 + 수비 가중치(1.25배) 적용
        float offensePower = (shooterRating.layup + shooterRating.drivingDunk) / 2f;

        // 성공률: 기존 로직에서 10% 페널티 + 수비 가중치(1.25배) 적용
        float successChance = 45f + (offensePower - (defenderRating.interiorDefense * 1.25f)) * 0.9f;
        successChance = UnityEngine.Mathf.Clamp(successChance, 10f, 85f);
        
        sim.AddLog($"FORCED drive by {player.Rating.name} past {defender.Rating.name}. Chance: {successChance:F1}%");

        if (UnityEngine.Random.Range(0, 100) < successChance)
        {
            sim.CurrentState.HomeScore += (player.TeamId == 0) ? 2 : 0;
            sim.CurrentState.AwayScore += (player.TeamId == 1) ? 2 : 0;
            player.Stats.Points += 2;
            player.Stats.FieldGoalsMade++;
            sim.RecordAssist(sim.CurrentState.LastPasser);
            sim.UpdatePlusMinusOnScore(player.TeamId, 2);
            sim.AddLog("Scores!");

            // 공격권 전환 및 리셋
            sim.CurrentState.PossessingTeamId = 1 - player.TeamId;
            sim.CurrentState.ShotClockSeconds = 24f;
            sim.CurrentState.CurrentBallHandler = null;
            sim.CurrentState.LastPasser = null;
        }
        else
        {
            sim.AddLog("Missed the layup under pressure.");
            sim.ResolveRebound(player);
            sim.CurrentState.CurrentBallHandler = null;
        }
        sim.ConsumeTime(UnityEngine.Random.Range(5f, 9f)); // 공격 시간 소모 4-7초 -> 5-9초로 증가
        return NodeState.SUCCESS;
    }
}

public class Action_TryForcedMidRangeShot : Node
{
    public override NodeState Evaluate(GameSimulator sim, GameSimulator.GamePlayer player)
    {
        player.Stats.FieldGoalsAttempted++;
        var defender = sim.GetRandomDefender(player.TeamId);
        var shooterRating = sim.GetAdjustedRating(player);
        var defenderRating = sim.GetAdjustedRating(defender);

        // 성공률: 기존 로직에서 10% 페널티 + 수비 가중치(1.2배) 적용
        float successChance = 40f + (shooterRating.midRangeShot - (defenderRating.perimeterDefense * 1.2f)) * 0.85f;
        successChance = UnityEngine.Mathf.Clamp(successChance, 15f, 80f);

        sim.AddLog($"FORCED mid-range by {player.Rating.name} against {defender.Rating.name}. Chance: {successChance:F1}%");
        
        if (UnityEngine.Random.Range(0, 100) < successChance)
        {
            sim.CurrentState.HomeScore += (player.TeamId == 0) ? 2 : 0;
            sim.CurrentState.AwayScore += (player.TeamId == 1) ? 2 : 0;
            player.Stats.Points += 2;
            player.Stats.FieldGoalsMade++;
            sim.RecordAssist(sim.CurrentState.LastPasser);
            sim.UpdatePlusMinusOnScore(player.TeamId, 2);
            sim.AddLog("Swish.");
            
            // 공격권 전환 및 리셋
            sim.CurrentState.PossessingTeamId = 1 - player.TeamId;
            sim.CurrentState.ShotClockSeconds = 24f;
            sim.CurrentState.CurrentBallHandler = null;
            sim.CurrentState.LastPasser = null;
        }
        else
        {
            sim.AddLog("Clanks off the rim.");
            sim.ResolveRebound(player);
            sim.CurrentState.CurrentBallHandler = null;
        }
        sim.ConsumeTime(UnityEngine.Random.Range(4f, 8f)); // 공격 시간 소모 3-6초 -> 4-8초로 증가
        return NodeState.SUCCESS;
    }
}