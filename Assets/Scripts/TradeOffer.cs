using System.Collections.Generic;

/// <summary>
/// AI가 유저에게 제안하는 트레이드 정보를 담는 클래스.
/// </summary>
public class TradeOffer
{
    public Team ProposingTeam { get; }
    public Team TargetTeam { get; } // User's team
    public List<PlayerRating> PlayersOfferedByProposingTeam { get; }
    public List<PlayerRating> PlayersRequestedFromTargetTeam { get; }
    public bool IsUserInvolved { get; }

    public TradeOffer(Team proposingTeam, List<PlayerRating> offeredPlayers, Team targetTeam, List<PlayerRating> requestedPlayers, bool isUserInvolved = true)
    {
        ProposingTeam = proposingTeam;
        PlayersOfferedByProposingTeam = offeredPlayers;
        TargetTeam = targetTeam;
        PlayersRequestedFromTargetTeam = requestedPlayers;
        IsUserInvolved = isUserInvolved;
    }
} 