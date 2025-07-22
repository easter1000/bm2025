using UnityEngine;

/// <summary>
/// 씬과 씬 사이에 경기 정보를 전달하기 위한 정적 데이터 홀더입니다.
/// </summary>
public static class GameDataHolder
{
    public static Schedule CurrentGameInfo { get; set; }
}