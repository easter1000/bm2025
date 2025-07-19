using System;

namespace madcamp3.Assets.Script.Player
{
    /// <summary>
    /// 선수 한 명의 기본 정보를 담는 데이터 구조체.
    /// 외부 스크립트에서도 접근할 수 있도록 public, Serializable 로 선언합니다.
    /// </summary>
    [Serializable]
    public class PlayerLine
    {
        public string Position;
        public int BackNumber;
        public string PlayerName;
        public int Age;
        public string Height;   // 예: "6'3\""
        public int Weight;      // 파운드(lb) 단위
        public int OverallScore; // 종합 능력치 (0~99)
        public int Potential;    // 잠재력 (0~99)
    }
} 