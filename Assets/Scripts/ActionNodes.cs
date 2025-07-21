using UnityEngine;
using System.Linq;

// --- 조건 노드: 3점슛을 쏠 만한 상황인가? ---
public class Condition_IsOpenFor3 : ConditionNode
{
    public override NodeState Evaluate(GameSimulator simulator, GamePlayer player)
    {
        var adjustedRating = simulator.GetAdjustedRating(player);

        float tendency = 25f; 
        tendency += (player.Rating.overallAttribute - 85) * 0.25f;

        bool willTry3Pointer = adjustedRating.threePointShot > 75 && Random.Range(0, 100) < tendency;
        if (willTry3Pointer) return NodeState.SUCCESS;
        return NodeState.FAILURE;
    }
}

// --- 조건 노드: 돌파가 가능한가? ---
public class Condition_CanDrive : ConditionNode
{
    public override NodeState Evaluate(GameSimulator simulator, GamePlayer player)
    {
        var adjustedRating = simulator.GetAdjustedRating(player);

        float driveTendency = 20 + (adjustedRating.drivingDunk + adjustedRating.layup) / 2.0f * 0.3f;
        driveTendency += (player.Rating.overallAttribute - 85) * 0.3f;
        if (Random.Range(0, 100) >= driveTendency) return NodeState.FAILURE;
        
        var defender = simulator.GetRandomDefender(player.TeamId);
        var adjustedDefenderRating = simulator.GetAdjustedRating(defender);

        float successChance = 50 + (adjustedRating.ballHandle + adjustedRating.speed) / 2f - adjustedDefenderRating.perimeterDefense;
        successChance = Mathf.Clamp(successChance, 10, 90);
        if (Random.Range(0, 100) < successChance) return NodeState.SUCCESS;
        return NodeState.FAILURE;
    }
}

// --- 조건 노드: 결정적인 패스 기회가 있는가? ---
public class Condition_IsGoodPassOpportunity : ConditionNode
{
    public override NodeState Evaluate(GameSimulator simulator, GamePlayer player)
    {
        var teammates = simulator.GetPlayersOnCourt(player.TeamId).Where(p => p != player).ToList();
        if (teammates.Count == 0) return NodeState.FAILURE;
        
        var bestTeammate = teammates.OrderByDescending(p => p.Rating.overallAttribute).First();

        float passValue = (bestTeammate.Rating.overallAttribute - player.Rating.overallAttribute) * 0.5f;
        passValue += (player.Rating.passIQ - 85) * 0.2f;

        if (passValue > Random.Range(3f, 6f))
        {
            return NodeState.SUCCESS;
        }

        return NodeState.FAILURE;
    }
}


// --- 조건 노드: 미드레인지 슛을 쏠 만한 상황인가? ---
public class Condition_IsGoodForMidRange : ConditionNode
{
    public override NodeState Evaluate(GameSimulator simulator, GamePlayer player)
    {
        var adjustedRating = simulator.GetAdjustedRating(player);
        
        float tendency = 34f;
        tendency += (player.Rating.overallAttribute - 85) * 0.25f;

        bool willTry = adjustedRating.midRangeShot > 70 && Random.Range(0, 100) < tendency;
        if (willTry)
        {
            return NodeState.SUCCESS;
        }
        return NodeState.FAILURE;
    }
}


// --- 액션 노드: 3점슛 시도 ---
public class Action_Try3PointShot : ActionNode
{
    public override NodeState Evaluate(GameSimulator simulator, GamePlayer player)
    {
        var adjustedRating = simulator.GetAdjustedRating(player);
        var defender = simulator.GetRandomDefender(player.TeamId);
        var adjustedDefenderRating = simulator.GetAdjustedRating(defender);
        simulator.ConsumeTime(Random.Range(2, 5));

        float foulChance = 10 + (adjustedRating.drawFoul - adjustedDefenderRating.perimeterDefense) / 4f;
        if (Random.Range(0, 100) < foulChance)
        {
            return simulator.ResolveShootingFoul(player, defender, 3);
        }
        
        player.Stats.FieldGoalsAttempted++;
        player.Stats.ThreePointersAttempted++;

        float baseChance = 36f;
        float ratingModifier = (adjustedRating.threePointShot - 75) * 0.5f - (adjustedDefenderRating.perimeterDefense - 75) * 0.5f;
        
        float passIQBonus = 0f;
        if (simulator.CurrentState.LastPasser != null)
        {
            float passerIQ = simulator.CurrentState.LastPasser.Rating.passIQ;
            if (passerIQ > 75)
            {
                passIQBonus = (passerIQ - 75) * 0.2f; // passIQ 75-99 -> 0-4.8 bonus
            }
        }
        
        float successChance = Mathf.Clamp(baseChance + ratingModifier + passIQBonus, 15f, 65f);

        if (Random.Range(0, 100) < successChance)
        {
            player.Stats.Points += 3;
            player.Stats.FieldGoalsMade++;
            player.Stats.ThreePointersMade++;
            
            string description = $"{player.Rating.name} makes a three point jumper.";
            if (simulator.CurrentState.LastPasser != null)
            {
                simulator.CurrentState.LastPasser.Stats.Assists++;
                description += $" (assist by {simulator.CurrentState.LastPasser.Rating.name})";
            }
            if (player.TeamId == 0) simulator.CurrentState.HomeScore += 3; else simulator.CurrentState.AwayScore += 3;
            simulator.AddLog(description);
            simulator.CurrentState.LastPasser = null;
            simulator.CurrentState.PossessingTeamId = 1 - player.TeamId;
        }
        else
        {
            simulator.AddLog($"{player.Rating.name} misses a three point jumper.");
            simulator.ResolveRebound(player);
        }
        return NodeState.SUCCESS;
    }
}

// --- 액션 노드: 돌파 후 마무리 ---
public class Action_DriveAndFinish : ActionNode
{
    public override NodeState Evaluate(GameSimulator simulator, GamePlayer player)
    {
        var adjustedRating = simulator.GetAdjustedRating(player);
        var interiorDefender = simulator.GetRandomDefender(player.TeamId);
        var adjustedDefenderRating = simulator.GetAdjustedRating(interiorDefender);
        simulator.ConsumeTime(Random.Range(3, 6));

        float foulChance = 15 + (adjustedRating.drawFoul - adjustedDefenderRating.interiorDefense) / 4f;
        if (Random.Range(0, 100) < foulChance)
        {
            return simulator.ResolveShootingFoul(player, interiorDefender, 2);
        }
        
        player.Stats.FieldGoalsAttempted++;
        float baseChance = 55f;
        float offensePower = (adjustedRating.layup + adjustedRating.drivingDunk) / 2f;
        float defensePower = (adjustedDefenderRating.interiorDefense + adjustedDefenderRating.block) / 2f;
        float ratingModifier = (offensePower - 75) - (defensePower - 75);

        float passIQBonus = 0f;
        if (simulator.CurrentState.LastPasser != null)
        {
            float passerIQ = simulator.CurrentState.LastPasser.Rating.passIQ;
            if (passerIQ > 75)
            {
                passIQBonus = (passerIQ - 75) * 0.2f;
            }
        }

        float finishChance = Mathf.Clamp(baseChance + ratingModifier + passIQBonus, 25f, 95f);

        if (Random.Range(0, 100) < finishChance)
        {
            player.Stats.Points += 2;
            player.Stats.FieldGoalsMade++;
            
            string description = $"{player.Rating.name} drives and makes a layup.";
            if (simulator.CurrentState.LastPasser != null)
            {
                simulator.CurrentState.LastPasser.Stats.Assists++;
                description += $" (assist by {simulator.CurrentState.LastPasser.Rating.name})";
            }
            if (player.TeamId == 0) simulator.CurrentState.HomeScore += 2; else simulator.CurrentState.AwayScore += 2;
            simulator.AddLog(description);
            simulator.CurrentState.LastPasser = null;
            simulator.CurrentState.PossessingTeamId = 1 - player.TeamId;
        }
        else
        {
            simulator.AddLog($"{player.Rating.name} misses the layup.");
            simulator.ResolveRebound(player);
        }
        return NodeState.SUCCESS;
    }
}

// --- 액션 노드: 미드레인지 슛 시도 ---
public class Action_TryMidRangeShot : ActionNode
{
    public override NodeState Evaluate(GameSimulator simulator, GamePlayer player)
    {
        var adjustedRating = simulator.GetAdjustedRating(player);
        var defender = simulator.GetRandomDefender(player.TeamId);
        var adjustedDefenderRating = simulator.GetAdjustedRating(defender);
        simulator.ConsumeTime(Random.Range(3, 6));

        float foulChance = 12 + (adjustedRating.drawFoul - adjustedDefenderRating.perimeterDefense) / 4f;
        if (Random.Range(0, 100) < foulChance)
        {
            return simulator.ResolveShootingFoul(player, defender, 2);
        }

        player.Stats.FieldGoalsAttempted++;
        float baseChance = 42f;
        float ratingModifier = (adjustedRating.midRangeShot - 75) * 0.5f - (adjustedDefenderRating.perimeterDefense - 75) * 0.5f;

        float passIQBonus = 0f;
        if (simulator.CurrentState.LastPasser != null)
        {
            float passerIQ = simulator.CurrentState.LastPasser.Rating.passIQ;
            if (passerIQ > 75)
            {
                passIQBonus = (passerIQ - 75) * 0.2f;
            }
        }

        float successChance = Mathf.Clamp(baseChance + ratingModifier + passIQBonus, 20f, 75f);

        if (Random.Range(0, 100) < successChance)
        {
            player.Stats.Points += 2;
            player.Stats.FieldGoalsMade++;

            string description = $"{player.Rating.name} makes a pullup jump shot.";
            if (simulator.CurrentState.LastPasser != null)
            {
                simulator.CurrentState.LastPasser.Stats.Assists++;
                description += $" (assist by {simulator.CurrentState.LastPasser.Rating.name})";
            }
            if (player.TeamId == 0) simulator.CurrentState.HomeScore += 2; else simulator.CurrentState.AwayScore += 2;
            simulator.AddLog(description);
            simulator.CurrentState.LastPasser = null;
            simulator.CurrentState.PossessingTeamId = 1 - player.TeamId;
        }
        else
        {
            simulator.AddLog($"{player.Rating.name} misses a pullup jump shot.");
            simulator.ResolveRebound(player);
        }
        return NodeState.SUCCESS;
    }
}

// --- 액션 노드: 가장 좋은 동료에게 패스 ---
public class Action_PassToBestTeammate : ActionNode
{
    public override NodeState Evaluate(GameSimulator simulator, GamePlayer player)
    {
        var teammates = simulator.GetPlayersOnCourt(player.TeamId).Where(p => p != player).ToList();
        if (teammates.Count == 0) return NodeState.FAILURE;

        var bestTeammate = teammates.OrderByDescending(p => {
            float score = p.Rating.overallAttribute * 0.7f;
            score += p.Rating.passIQ > 75 ? p.Rating.threePointShot : p.Rating.midRangeShot;
            return score;
        }).First();

        simulator.ConsumeTime(Random.Range(1, 3));
        simulator.AddLog($"{player.Rating.name} passes the ball to {bestTeammate.Rating.name}.");
        
        // [핵심 수정]
        // 1. "내가 패스했어" 라고 LastPasser에 기록
        simulator.CurrentState.LastPasser = player;
        
        // 2. 패스는 성공했고, 즉시 패스 받은 선수(bestShooter)를 대상으로 
        //    행동 트리의 최상단(루트 노드)부터 평가를 다시 시작함
        //    이것이 패스와 슛을 하나의 연속된 행동으로 묶어주는 역할을 함
        return simulator.GetRootNode().Evaluate(simulator, bestTeammate);
    }
}


// --- 액션 노드: 자유투 실행 ---
public class Action_ShootFreeThrows : ActionNode
{
    private GamePlayer _shooter;
    private int _numberOfShots;

    public Action_ShootFreeThrows(GamePlayer shooter, int numberOfShots)
    {
        _shooter = shooter;
        _numberOfShots = numberOfShots;
    }

    public override NodeState Evaluate(GameSimulator simulator, GamePlayer player)
    {
        bool lastShotMade = false;
        for (int i = 0; i < _numberOfShots; i++)
        {
            simulator.ConsumeTime(Random.Range(4, 8));
            _shooter.Stats.FreeThrowsAttempted++;

            bool isSuccess = Random.Range(0, 100) < _shooter.Rating.freeThrow;
            if (isSuccess)
            {
                _shooter.Stats.FreeThrowsMade++;
                _shooter.Stats.Points++;
                if (_shooter.TeamId == 0) simulator.CurrentState.HomeScore++; else simulator.CurrentState.AwayScore++;
                simulator.AddLog($"{_shooter.Rating.name} makes free throw ({i + 1} of {_numberOfShots}).");
                lastShotMade = true;
            }
            else
            {
                simulator.AddLog($"{_shooter.Rating.name} misses free throw ({i + 1} of {_numberOfShots}).");
                lastShotMade = false;
            }
        }

        if (lastShotMade)
        {
            simulator.CurrentState.PossessingTeamId = 1 - _shooter.TeamId;
        }
        else
        {
            simulator.AddLog("Rebound after missed free throw.");
            simulator.ResolveRebound(_shooter);
        }
        
        simulator.CurrentState.LastPasser = null;
        
        return NodeState.SUCCESS;
    }
}