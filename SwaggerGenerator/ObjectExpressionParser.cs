using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SwaggerGenerator;

public class ObjectExpressionParser
{
    /// <summary>
    ///     
    /// </summary>
    /// <param name="parseClassFilePath"></param>
    /// <param name="targetIdentifier"></param>
    /// <param name="isOuter"></param>
    /// <returns></returns>
    public string? Execute(string parseClassFilePath, string targetIdentifier, bool isOuter = false)
    {
        var targetCode = File.ReadAllText(parseClassFilePath);
        
        // コードを解析して構文木を作成
        var tree = CSharpSyntaxTree.ParseText(targetCode);
        
        // 構文木の頂点ノード取得
        var rootNode = tree.GetRoot();

        // 代入演算子を持つ式のリスト
        // Body = new RequestInfo(),PlayerId = 123など
        var assignmentExpressionSyntaxes = rootNode
            .DescendantNodes()
            .OfType<AssignmentExpressionSyntax>();

        ObjectCreationExpressionSyntax? bodyCreation;
        if (isOuter == false)
        {
            // 識別子 (identifier)がBodyの箇所を取得
            // int myValue = 10;で言うmyValueを指す。
            var objectCreation = assignmentExpressionSyntaxes
                .FirstOrDefault(a => a.Left is IdentifierNameSyntax id &&
                                     id.Identifier.Text == targetIdentifier);
            
            // オブジェクト生成式の構文ノードの型へ変換。
            // Bodyオブジェクトのイニシャライザ式を配列のように扱うようにするため。
            bodyCreation =  (ObjectCreationExpressionSyntax)objectCreation.Right;
        }
        else
        {
            bodyCreation = rootNode
                .DescendantNodes()
                .OfType<ObjectCreationExpressionSyntax>()
                .FirstOrDefault(x => x.Type.ToString() == targetIdentifier);
        }

        if (bodyCreation.Initializer is null) return null;

        var parsed = ParseInitializer(bodyCreation.Initializer);
            
        var jsonOptions = new JsonSerializerOptions
        {
            // デフォルトではなく UnsafeRelaxedJsonEscaping を使う
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true  // インデントするなら（任意）
        };
            
        var json = JsonSerializer.Serialize(parsed, jsonOptions);

        return json;
    }
    
    /// <summary>
    ///     初期化子の解析
    /// </summary>
    /// <param name="initializer"></param>
    /// <returns></returns>
    private object ParseInitializer(InitializerExpressionSyntax initializer)
    {
        var result = new Dictionary<string, object?>();

        foreach (var expression in initializer.Expressions)
        {
            if (expression is AssignmentExpressionSyntax assignment)
            {
                var key = assignment.Left.ToString();
                var value = assignment.Right;

                result[key] = ParseExpressionValue(value);
            }
        }

        return result;
    }
    
    /// <summary>
    ///     式の値の解析
    /// </summary>
    /// <param name="expr"></param>
    /// <returns></returns>
    private object? ParseExpressionValue(ExpressionSyntax expr)
    {
        switch (expr)
        {
            // リテラルを記述している箇所に対応
            // int x = 10, string s = "hello"
            case LiteralExpressionSyntax literal:
                return ParseLiteral(literal);

            // メソッド呼び出しやデリゲート呼び出しを行っている箇所に対応
            // Console.WriteLine("Hello");, someObject.DoSomething(10);
            case InvocationExpressionSyntax invocation:
                return ParseInvocation(invocation);

            // new キーワードを使って 新しいインスタンスを生成している箇所に対応
            // var obj = new SomeClass() { PropA = 1, PropB = "abc" };
            // var numbers = new List<int> { 1, 2, 3 };
            // var person = new Person
            // {
            //     Name = "Bob", 
            //     Age = 30
            // }; 
            case ObjectCreationExpressionSyntax objCreation:
                if (objCreation.Initializer != null)
                {
                    // List<T>の場合
                    if (objCreation.Initializer.Expressions.All(e =>
                            e is ObjectCreationExpressionSyntax ||
                            e is ImplicitObjectCreationExpressionSyntax ||
                            e is LiteralExpressionSyntax))
                    {
                        var list = new List<object>();
                        foreach (var expression in objCreation.Initializer.Expressions)
                        {
                            var returnResult = ParseExpressionValue(expression);
                            list.Add(returnResult);
                        }
                        
                        return list;
                    }

                    // 初期化子の中身が「プロパティ (またはフィールド) への代入式」 になっている場合
                    // new Person
                    // {
                    //     Name = "Alice",
                    //     Age = 20
                    // }
                    if (objCreation.Initializer.Expressions.All(e => e is AssignmentExpressionSyntax))
                    {
                        var dictionary = new Dictionary<object, object>() { };
                        foreach (var expressionSyntax in objCreation.Initializer.Expressions)
                        {
                            var vExpression = (AssignmentExpressionSyntax)expressionSyntax;
                            var key = vExpression.Left.ToString();
                            var value = ParseExpressionValue(vExpression.Right);

                            dictionary[key] = value;
                        }

                        return dictionary;
                    }

                    // 初期化子の要素がさらに「初期化子式 { ... }」である 場合
                    // new Dictionary<string, string>
                    // {
                    //     { "key1", "value1" },
                    //     { "key2", "value2" }
                    // }
                    if (objCreation.Initializer.Expressions.All(e => e is InitializerExpressionSyntax))
                    {
                        var dictionary = new Dictionary<object, object?>() { };
                        foreach (var expressionSyntax in objCreation.Initializer.Expressions)
                        {
                            var syntax = (InitializerExpressionSyntax)expressionSyntax;

                            var keyExpr = syntax.Expressions[0];
                            var valueExpr = syntax.Expressions[1];;
                            
                            var key = ParseExpressionValue(keyExpr).ToString();
                            var value = ParseExpressionValue(valueExpr).ToString();

                            if (key != null)
                            {
                                dictionary[key] = value;
                            }
                        }

                        return dictionary;
                    }


                    return ParseInitializer(objCreation.Initializer);
                }
                break;
            
            // new ()の形
            case ImplicitObjectCreationExpressionSyntax implicitObjectCreation:

                if (implicitObjectCreation.Initializer is null)
                {
                    var implicitObjectList = new List<object?>();

                    foreach (var argumentSyntax in implicitObjectCreation.ArgumentList.Arguments)
                    {
                        var value = ParseExpressionValue(argumentSyntax.Expression);
                        implicitObjectList.Add(value);
                    }

                    return implicitObjectList;
                }
                else
                {
                    var dictionary = new Dictionary<object, object>() { };

                    foreach (var expression in implicitObjectCreation.Initializer.Expressions)
                    {
                        {
                            var vExpression = (AssignmentExpressionSyntax)expression;
                            var key = vExpression.Left.ToString();
                            var value = ParseExpressionValue(vExpression.Right);

                            dictionary[key] = value;
                        }
                    }
                    
                    return dictionary;
                }


            // 代入式（= や += などの代入・複合代入演算子）が登場する箇所に対応
            // x = 10, y += 3;
            // case AssignmentExpressionSyntax assignment:
            //     // 再帰的に中のデータを処理
            //     var dict = new Dictionary<string, object>
            //     {
            //         { assignment.Left.ToString(), ParseValue(assignment.Right) }
            //     };
            //     return dict;
        }

         return null;
    }
    
    /// <summary>
    ///     リテラルの解析
    /// </summary>
    /// <param name="literal"></param>
    /// <returns></returns>
    private object? ParseLiteral(LiteralExpressionSyntax literal)
    {
        switch (literal.Kind())
        {
            // null
            case SyntaxKind.NullLiteralExpression:
                return null;
            
            // 文字列
            case SyntaxKind.StringLiteralExpression:
                return literal.Token.ValueText;
            
            // 数値
            case SyntaxKind.NumericLiteralExpression:
                return literal.Token.Value;
                
            // True
            case SyntaxKind.TrueLiteralExpression:
                return true;
            
            // False
            case SyntaxKind.FalseLiteralExpression:
                return false;
            
            default:
                return literal.ToString();
        }
    }
    
    /// <summary>
    ///     関数・メソッドの解析
    ///     現状はDateTime.Parseのみ
    /// </summary>
    /// <param name="invocation"></param>
    /// <returns></returns>
    private object? ParseInvocation(InvocationExpressionSyntax invocation)
    {
        return ParseExpressionValue(invocation.ArgumentList.Arguments[0].Expression);
    }
}