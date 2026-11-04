using System;
using System.Collections.Generic;

namespace SkillDsl;

/// <summary>
/// 从一棵表达式树里提取它引用到的变量路径。
/// </summary>
/// <remarks>
/// <para>用途是<b>依赖收集</b>（UI 绑定的脏检查、DSL 的失效重算）：调用方按返回路径取第一段得到"根"，
/// 根对应的数据变化时才重算，而不是每次都全量求值。</para>
/// <para>本类只做<b>纯文本层面的提取</b>，不认识任何具体变量——与
/// <see cref="ExpressionEvaluator"/> 一样，路径的语义完全由宿主决定。</para>
/// </remarks>
public static class ExpressionVariables
{
	/// <summary>提取全部变量路径（去重，保持首次出现顺序）。</summary>
	/// <param name="expression">待分析的表达式。</param>
	/// <returns>变量路径列表；没有变量时为空列表。</returns>
	/// <exception cref="ArgumentNullException">表达式为 null。</exception>
	public static IReadOnlyList<string> CollectPaths(Expression expression)
	{
		ArgumentNullException.ThrowIfNull(expression);

		List<string> ordered = [];
		HashSet<string> seen = new(StringComparer.Ordinal);
		Walk(expression, ordered, seen);
		return ordered;
	}

	/// <summary>提取全部变量路径的<b>根</b>（第一段，去重且保持顺序）。</summary>
	/// <param name="expression">待分析的表达式。</param>
	/// <exception cref="ArgumentNullException">表达式为 null。</exception>
	public static IReadOnlyList<string> CollectRoots(Expression expression)
	{
		IReadOnlyList<string> paths = CollectPaths(expression);

		if (paths.Count == 0)
		{
			return [];
		}

		List<string> roots = [];
		HashSet<string> seen = new(StringComparer.Ordinal);

		foreach (string path in paths)
		{
			string root = RootOf(path);

			if (seen.Add(root))
			{
				roots.Add(root);
			}
		}

		return roots;
	}

	/// <summary>取路径的第一段（根）。路径没有点号时，整段就是根。</summary>
	/// <param name="path">点分路径，如 <c>unit.hp</c>。</param>
	/// <exception cref="ArgumentException">路径为空。</exception>
	public static string RootOf(string path)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(path);

		int dot = path.IndexOf('.', StringComparison.Ordinal);
		return dot < 0 ? path : path[..dot];
	}

	private static void Walk(Expression expression, List<string> ordered, HashSet<string> seen)
	{
		switch (expression)
		{
			case VariableExpression variable:
				if (seen.Add(variable.Path))
				{
					ordered.Add(variable.Path);
				}

				return;

			case UnaryExpression unary:
				Walk(unary.Operand, ordered, seen);
				return;

			case BinaryExpression binary:
				Walk(binary.Left, ordered, seen);
				Walk(binary.Right, ordered, seen);
				return;

			case CallExpression call:
				foreach (Expression argument in call.Arguments)
				{
					Walk(argument, ordered, seen);
				}

				return;

			default:
				// 字面量没有变量；将来新增节点类型时这里静默跳过而不是崩——
				// 漏收集只会让重算变少（性能问题），不会算错值。
				return;
		}
	}
}
