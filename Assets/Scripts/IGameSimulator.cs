using System.Collections.Generic;

// 두 시뮬레이터 (GameSimulator, BackgroundGameSimulator)가 공유하는 기능에 대한 인터페이스
public interface IGameSimulator 
{
    // 공유 속성 - GameState는 GamaData.cs의 것을 사용
    GameState CurrentState { get; }

    // 공유 메서드 - GamePlayer는 GamaData.cs의 것을 사용
    void AddLog(string description);
    void AddUILog(string message); // UI 로그를 위한 메서드 추가
    void RecordAssist(GamePlayer passer);
    void UpdatePlusMinusOnScore(int scoringTeamId, int points);
    void ConsumeTime(float seconds);
    List<GamePlayer> GetPlayersOnCourt(int teamId);
    List<GamePlayer> GetAllPlayersOnCourt();
    GamePlayer GetRandomDefender(int attackingTeamId);
    NodeState ResolveShootingFoul(GamePlayer shooter, GamePlayer defender, int freeThrows);
    void ResolveRebound(GamePlayer shooter);
    void ResolveTurnover(GamePlayer offensivePlayer, GamePlayer defensivePlayer, bool isSteal); // 스틸/턴오버 처리
    void ResolveBlock(GamePlayer shooter, GamePlayer blocker); // 블락 처리
    PlayerRating GetAdjustedRating(GamePlayer player);
} 