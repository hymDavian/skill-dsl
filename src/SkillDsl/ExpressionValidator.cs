namespace SkillDsl;

/// <summary>一条表达式静态检查问题。</summary>
public sealed record ScriptIssue(string Code, string Message);

/// <summary>
/// 表达式的静态检查：<b>不求值</b>，找出一定会失败的地方。
/// </summary>
/// <remarks>
/// 与 <see cref="ExpressionEvaluator"/> 的分工：
/// 求值器在<b>运行时</b>发现"这次数据有问题"；本校验器在<b>加载期</b>发现"这份数据永远跑不通"。
/// 前者报一条就中断，后者<b>遍历整棵 AST 收集全部问题</b>（对齐项目的"校验一次报全"约束）。
/// </remarks>
public static class ExpressionValidator
{
	/// <summary>错误码：调用了白名单之外的函数。</summary>
	public const string UnknownFunction = "E3101";

	/// <summary>错误码：函数参数个数不在允许区间。</summary>
	public const string ArityMismatch = "E3102";

	/// <summary>检查一棵表达式树。</summary>
	/// <param name="expression">待检查的表达式。</param>
	/// <param name="functions">当前可用的函数白名单。</param>
	/// <returns>全部问题；为空表示通过。</returns>
	public static IReadOnlyList<ScriptIssue> Validate(Expression expression, ScriptFunctionTable functions)
	{
		ArgumentNullException.ThrowIfNull(expression);
		ArgumentNullException.ThrowIfNull(functions);

		List<ScriptIssue> issues = new();
		Walk(expression, functions, issues);
		return issues;
	}

	private static void Walk(Expression expression, ScriptFunctionTable functions, List<ScriptIssue> issues)
	{
		switch (expression)
		{
			case LiteralExpression or VariableExpression:
				return;

			case UnaryExpression unary:
				Walk(unary.Operand, functions, issues);
				return;

			case BinaryExpression binary:
				Walk(binary.Left, functions, issues);
				Walk(binary.Right, functions, issues);
				return;

			case CallExpression call:
				ValidateCall(call, functions, issues);
				foreach (Expression argument in call.Arguments)
				{
					Walk(argument, functions, issues);
				}

				return;
		}
	}

	private static void ValidateCall(CallExpression call, ScriptFunctionTable functions, List<ScriptIssue> issues)
	{
		if (!functions.TryGet(call.Name, out ScriptFunction? function) || function is null)
		{
			issues.Add(new ScriptIssue(UnknownFunction, $"未知函数：{call.Name}"));
			return;
		}

		int count = call.Arguments.Count;
		if (count < function.MinArguments || count > function.MaxArguments)
		{
			issues.Add(new ScriptIssue(
				ArityMismatch,
				$"函数 {call.Name} 需要 {function.MinArguments}~{function.MaxArguments} 个参数，实际传入 {count} 个"));
		}
	}
}
