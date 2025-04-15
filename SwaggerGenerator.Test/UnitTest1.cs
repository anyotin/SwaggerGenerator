using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit.Abstractions;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace SwaggerGenerator.Test;

public class UnitTest1
{
    private readonly ITestOutputHelper _testOutputHelper;
    private string TestJson { get;}
    
    public UnitTest1(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
        
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
        
        var jsonOption = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
        
        TestJson = FormatJson(JsonConvert.SerializeObject(request, jsonOption));
    }
    
    [Fact]
    public void Test1()
    {
        var objectExpressionParser = new ObjectExpressionParser();
        
        var currentPath =  new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.FullName;

        var result = objectExpressionParser.Execute(currentPath + "/TestController.cs", "TestRequest", true);
        
        Assert.Equal(TestJson, result);
    }
    
    /// <summary>
    ///     シリアライズされたJson文字列の整形
    /// </summary>
    /// <param name="json"></param>
    /// <returns></returns>
    private static string FormatJson(string json)
    {
        dynamic parsedJson = JsonConvert.DeserializeObject(json);
        return JsonConvert.SerializeObject(parsedJson, Formatting.Indented);
    }
}