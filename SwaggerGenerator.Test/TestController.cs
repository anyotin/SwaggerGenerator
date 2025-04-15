namespace SwaggerGenerator.Test;

public class TestController
{
    public void Test1()
    {
        var request = new TestRequest()
        {
            PlayerId = 12345,
            PlayerName = "Test",
            PlayerIds = new List<int>() { 12345, 5678, 123 },
            PlayerNames = new Dictionary<int, string>()
            {
                { 1, "Takeo" }, { 2, "Gosho" }, { 3, "Polilo" }
            },
            CommonBattleResult = new CommonBattleResult()
            {
                BattleId = 1,
                TeamScore = 999999999
            },
            PlayerBattleResults = new List<PlayerBattleResult>()
            {
                new()
                {
                    Score = 123,
                    TitleIds = new List<int>() { 1, 2, 3 },
                },
                new()
                {
                    Score = 333,
                    TitleIds = new List<int>() { 4, 5, 6 },
                }
            }
        };
    }
}