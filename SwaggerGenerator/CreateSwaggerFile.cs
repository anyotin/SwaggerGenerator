using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Abstracta.JmeterDsl.Core.ThreadGroups;
using static Abstracta.JmeterDsl.JmeterDsl;

namespace SwaggerGenerator;

public class CreateSwaggerFile
{
    /// <summary>
    ///     共通のエンドポイント箇所
    /// </summary>
    public const string CommonUrl = "http://localhost:5000";
    
    private string? SwaggerFileName { get; set; }
    
    private string swaggerFilePath = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.FullName + "/../Share/Ta/SwaggerYamls/";

    private string controllerTestPath = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.FullName + "/../Api.Tests/Controllers/";

    // jmxファイル出力
    // 出力先は/bin
    private const string SaveFileName = "sample.jmx";
    
    private string saveSourcePath = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory) + SaveFileName;

    private string saveTargetPath =
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "/Public/jmeter/sample.jmx";




    public CreateSwaggerFile()
    {
        
    }
    
    public class JmeterRequestInfo
    {
        /// <summary>
        ///  Api名称
        /// </summary>
        public string ApiName { get; set; }
        
        /// <summary>
        ///     エンドポイント
        /// </summary>
        public string Url { get; set; }
        
        /// <summary>
        ///     リクエストクラス名
        /// </summary>
        public string SchemaName { get; set; }
        
        /// <summary>
        ///     APIのカテゴリ(Battle, Friendなど)
        /// </summary>
        public string TagName { get; set; }
        
        /// <summary>
        ///     テストクラス名
        /// </summary>
        public string TestClassName { get; set; }
        
        /// <summary>
        ///     リクエストボディ
        /// </summary>
        public Dictionary<string, object> RequestBody = new ();
        
        public string RequsetBodyString { get; set; }
    }

    public void Execute()
    {
        // yamlファイル取得
        var swaggerFile = Directory.GetFiles(swaggerFilePath, "swagger_*.yaml");

        // 最新のyamlファイル取得
        var latestSwaggerFile = GetLatestFile(swaggerFile);
        
        // yamlファイルを読み込む
        string jsonString = File.ReadAllText(latestSwaggerFile);

        using var doc = JsonDocument.Parse(jsonString);
        var rootInfo = doc.RootElement; // ルート要素

        var requestList = new Dictionary<string, JmeterRequestInfo>();
        
        ExecPaths(rootInfo, requestList);
        ExecSchemas(rootInfo, requestList);
        ExecRequestParam(requestList);

        var groupChildren = new List<IThreadGroupChild>();
        
        // 共通のリクエストヘッダーをセット
        groupChildren.Add(HttpHeaders()
            .Header("x-ta-version", "v0.2.3")
            .Header("x-uuid", "c06a4f8d-d3b9-31ce-5b42-17e0f6d9453c")
            .Header("x-platform-user-id", "k-nakano")
            .Header("x-public-id", "RPVK-WMDS-XFHC-LTVB")
            .Header("x-platform", "31")
            .Header("x-platform-version", "123")
            .Header("x-model", "1")
            .Header("x-request-id", "340d8f6b-fc05-4a02-95fa-9bd9b5a630e6")
            .Header("x-appli-version", "1.0.0")
            .Header("x-timezone", "1")
            .Header("x-country", "JA")
            .Header("x-language", "ja")
            .Header("x-currency", "1")
            .Header("x-asset-revision", "1")
            .Header("x-db-revision", "2"));
        
        // 各APIをセット
        foreach (var request in requestList)
        {
            groupChildren.Add(HttpSampler(request.Value.ApiName, request.Value.Url).Post( request.Value.RequsetBodyString, new MediaTypeHeaderValue("application/x-json")));
        }
        
        TestPlan(ThreadGroup(1, 1, 
            groupChildren.ToArray())
        ).SaveAsJmx(SaveFileName);

        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        
        // 保存したファイルをJmeter実行場所へ移動
        File.Move(saveSourcePath, saveTargetPath, true);
    }
    
     /// <summary>
    ///     最新のyamlファイルを取得
    /// </summary>
    /// <param name="swaggerFiles"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    private string GetLatestFile(string[] swaggerFiles)
    {
        var latestFile = swaggerFiles
            .Select(filePath => new
            {
                Path = filePath,
                // ファイル名から `swagger_YYYYMMdd` 部分を切り出して DateTime に変換
                Date = DateTime.ParseExact(
                    Path.GetFileNameWithoutExtension(filePath).Split('_')[1],
                    "yyyyMMdd",
                    CultureInfo.InvariantCulture)
            })
            .OrderByDescending(x => x.Date)
            .FirstOrDefault();
        
        if (latestFile == null)
        {
            throw new ArgumentException("latestFile is null");
        }

        // 最新ファイルのパス
       return latestFile.Path;
    }

    /// <summary>
    ///     paths内のデータ処理
    /// </summary>
    /// <param name="rootInfo"></param>
    /// <param name="requestList">辞書型(参照型)なのでメソッド内での変更は保持される</param>
    private void ExecPaths(JsonElement rootInfo, Dictionary<string, JmeterRequestInfo> requestList)
    {
        var pathsElement = rootInfo.GetProperty("paths");

        foreach (var pathProperty in pathsElement.EnumerateObject())
        {
            var jmeterRequestInfo = new JmeterRequestInfo();
            
            jmeterRequestInfo.SchemaName = ConvertToRequest(pathProperty.Name);
            jmeterRequestInfo.Url = $"{CommonUrl}{pathProperty.Name}";
            
            // TODO 後で削除
            if ( ! jmeterRequestInfo.SchemaName.Contains("FriendRejectRequest")) continue;
            
            var postObject    = pathProperty.Value.GetProperty("post");
            
            jmeterRequestInfo.ApiName = postObject.GetProperty("summary").GetString();
            
            var tagName = postObject.GetProperty("tags")[0].GetString();
            jmeterRequestInfo.TagName = tagName;
            
            var schemaName = jmeterRequestInfo.SchemaName;
            jmeterRequestInfo.TestClassName = schemaName.Replace(tagName, "Test").Replace("Request", "Controller") + ".cs";
                    
            requestList.Add(jmeterRequestInfo.SchemaName, jmeterRequestInfo);
        }
        
        // ----------- 検証 -----------
        // 実行ソリューションのパス
        var currentDir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).Parent.Parent.Parent.FullName;

        // Swaggerファイルが置かれているディレクトリ
        var controllersDir = currentDir + "/../Api.Tests/Controllers/";

        var errorResultList = new List<string>();

        foreach (var v in requestList)
        {
            var path = controllersDir + v.Value.TagName + "/" + v.Value.TestClassName;

            if (!File.Exists(path))
            {
                errorResultList.Add(v.Value.TagName + "/" + v.Value.TestClassName);
            }
        }
    }
    
    /// <summary>
    ///     エンドポイントの文字列をリクエストクラスの文字列に変換
    /// </summary>
    /// <param name="endpoint"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    private string ConvertToRequest(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new ArgumentException("endpoint is null or empty.", nameof(endpoint));
        }

        // 1. 先頭・末尾の '/' を除去
        var trimmed = endpoint.Trim('/');

        // 2. '/' で分割
        var parts = trimmed.Split('/');

        // 3. "api" を除去
        parts = parts.Where(p => !p.Equals("api", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        // 4. 各要素をアッパーキャメルケース化して結合
        var pascalized = parts.Select(ToPascalCase);
        var combined = string.Join(string.Empty, pascalized);

        // 5. "Request" を最後に付与
        return combined + "Request";
    }
    
    /// <summary>
    ///     アッパーキャメルケース化
    /// </summary>
    /// <param name="text"></param>
    /// <returns></returns>
    private string ToPascalCase(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        if (text == "matchmaking")
        {
            return "MatchMaking";
        }

        if (text == "presentbox")
        {
            return "PresentBox";
        }
        
        return char.ToUpper(text[0]) + text.Substring(1).ToLower();
    }

    /// <summary>
    ///     schemas内のデータ処理
    /// </summary>
    /// <param name="rootInfo"></param>
    /// <param name="requestList"></param>
    private void ExecSchemas(JsonElement rootInfo, Dictionary<string, JmeterRequestInfo> requestList)
    {
        var schemasElement = rootInfo.GetProperty("components").GetProperty("schemas");

        foreach (var schemaProperty in schemasElement.EnumerateObject())
        {
            var keyName = schemaProperty.Name;
            
            // "properties" がなければ処理不要
            if (!schemaProperty.Value.TryGetProperty("properties", out var properties)) continue;

            // 該当するキーがなければ処理不要
            if (!requestList.TryGetValue(keyName, out var currentJmeterRequestInfo)) continue;
            
            foreach (var property in properties.EnumerateObject())
            {
                var type = property.Value.GetProperty("type").ToString();
                    
                var typeStr = "";
        
                if (type == "object")
                {
                    typeStr = "[]";
                }

                currentJmeterRequestInfo.RequestBody[property.Name] = typeStr;
            }
        }
    }

    private void ExecRequestParam(Dictionary<string, JmeterRequestInfo> requestList)
    {
        var objectExpressionParser = new ObjectExpressionParser();

        foreach (var request in requestList)
        {
            var path = controllerTestPath + request.Value.TagName + "/" + request.Value.TestClassName;

            try
            {
                request.Value.RequsetBodyString = objectExpressionParser.Execute(path);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}