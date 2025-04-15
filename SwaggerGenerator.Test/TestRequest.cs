namespace SwaggerGenerator.Test;

public class TestRequest
{
    public int PlayerId { get; set; }
    
    public string? PlayerName { get; set; }
    
    public List<int> PlayerIds { get; set; } = new List<int>();
    
    public Dictionary<int, string> PlayerNames { get; set; } = new Dictionary<int, string>();
    
    public CommonBattleResult? CommonBattleResult { get; set; }
    
    public List<PlayerBattleResult> PlayerBattleResults { get; set; } = new List<PlayerBattleResult>();
}

public class CommonBattleResult
{
    public int BattleId { get; set; }
    
    public string? BattleName { get; set; }

    public int TeamScore { get; set; } 
}

public class PlayerBattleResult
{
    public int Score { get; set; }
        
    public List<int> TitleIds { get; set; } = new List<int>();
}