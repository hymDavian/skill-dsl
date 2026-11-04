namespace SkillDsl;

/// <summary>
/// 表达式求值器。
/// </summary>
/// <remarks>
/// 求值规则（全部是硬约束，不做"聪明"的隐式处理）：
/// <list type="bullet">
/// <item>算术与比较只接受 Number；<c>&amp;&amp;</c> / <c>||</c> / <c>!</c> 只接受 Bool。</item>
/// <item><c>&amp;&amp;</c> / <c>||</c> <b>短路</b>：左侧已决定结果时不求值右侧。</item>
/// <item><c>==</c> / <c>!=</c> 要求两侧同类型；类型不同是错误，不是 false。</item>
/// <item>除零 / 取余零是错误，不返回 Infinity / NaN。</item>
/// </list>
/// </remarks>
public sealed class ExpressionEvaluator
{
	private readonly ScriptFunctionTable _functions;

	/// <summary>用一份函数白名单构造。</summary>
	public ExpressionEvaluator(ScriptFunctionTable functions)
	{
		ArgumentNullException.ThrowIfNull(functions);
		_functions = functions;
	}

	/// <summary>求值。</summary>
	/// <exception cref="ScriptRuntimeException">类型不符、除零、未知变量 / 函数等。</exception>
	public ScriptValue Evaluate(Expression expression, IScriptContext context)
	{
		ArgumentNullException.ThrowIfNull(expression);
		ArgumentNullException.ThrowIfNull(context);

		return EvaluateNode(expression, context);
	}

	private ScriptValue EvaluateNode(Expression expression, IScriptContext context)
		=> expression switch
		{
			LiteralExpression literal => literal.Value,
			VariableExpression variable => ResolveVariable(variable, context),
			UnaryExpression unary => EvaluateUnary(unary, context),
			BinaryExpression binary => EvaluateBinary(binary, context),
			CallExpression call => EvaluateCall(call, context),
			_ => throw new ScriptRuntimeException($"未支持的表达式节点：{expression.GetType().Name}"),
		};

	private static ScriptValue ResolveVariable(VariableExpression variable, IScriptContext context)
		=> context.TryGetVariable(variable.Path, out ScriptValue value)
			? value
			: throw new ScriptRuntimeException($"未知变量：{variable.Path}");

	private ScriptValue EvaluateUnary(UnaryExpression unary, IScriptContext context)
	{
		ScriptValue operand = EvaluateNode(unary.Operand, context);

		return unary.Operator switch
		{
			UnaryOperator.Negate => ScriptValue.FromNumber(-RequireNumber(operand, "-")),
			UnaryOperator.Not => ScriptValue.FromBool(!RequireBool(operand, "!")),
			_ => throw new ScriptRuntimeException($"未支持的一元运算符：{unary.Operator}"),
		};
	}

	private ScriptValue EvaluateBinary(BinaryExpression binary, IScriptContext context)
	{
		// 先处理短路运算符：右侧可能永远不会被求值（例如 false && (1/0 == 0)）。
		if (binary.Operator == BinaryOperator.And)
		{
			if (!RequireBool(EvaluateNode(binary.Left, context), "&&"))
			{
				return ScriptValue.FromBool(false);
			}

			return ScriptValue.FromBool(RequireBool(EvaluateNode(binary.Right, context), "&&"));
		}

		if (binary.Operator == BinaryOperator.Or)
		{
			if (RequireBool(EvaluateNode(binary.Left, context), "||"))
			{
				return ScriptValue.FromBool(true);
			}

			return ScriptValue.FromBool(RequireBool(EvaluateNode(binary.Right, context), "||"));
		}

		ScriptValue left = EvaluateNode(binary.Left, context);
		ScriptValue right = EvaluateNode(binary.Right, context);

		return binary.Operator switch
		{
			BinaryOperator.Add => ScriptValue.FromNumber(RequireNumber(left, "+") + RequireNumber(right, "+")),
			BinaryOperator.Subtract => ScriptValue.FromNumber(RequireNumber(left, "-") - RequireNumber(right, "-")),
			BinaryOperator.Multiply => ScriptValue.FromNumber(RequireNumber(left, "*") * RequireNumber(right, "*")),
			BinaryOperator.Divide => Divide(left, right),
			BinaryOperator.Modulo => Modulo(left, right),
			BinaryOperator.Equal => Comparison(left, right, "==", negate: false),
			BinaryOperator.NotEqual => Comparison(left, right, "!=", negate: true),
			BinaryOperator.Less => ScriptValue.FromBool(RequireNumber(left, "<") < RequireNumber(right, "<")),
			BinaryOperator.LessOrEqual => ScriptValue.FromBool(RequireNumber(left, "<=") <= RequireNumber(right, "<=")),
			BinaryOperator.Greater => ScriptValue.FromBool(RequireNumber(left, ">") > RequireNumber(right, ">")),
			BinaryOperator.GreaterOrEqual => ScriptValue.FromBool(RequireNumber(left, ">=") >= RequireNumber(right, ">=")),
			_ => throw new ScriptRuntimeException($"未支持的二元运算符：{binary.Operator}"),
		};
	}

	private ScriptValue EvaluateCall(CallExpression call, IScriptContext context)
	{
		if (!_functions.TryGet(call.Name, out ScriptFunction? function) || function is null)
		{
			throw new ScriptRuntimeException($"未知函数：{call.Name}");
		}

		int count = call.Arguments.Count;
		if (count < function.MinArguments || count > function.MaxArguments)
		{
			throw new ScriptRuntimeException(
				$"函数 {call.Name} 需要 {DescribeArity(function)} 个参数，实际传入 {count} 个");
		}

		ScriptValue[] arguments = new ScriptValue[count];
		for (int i = 0; i < count; i++)
		{
			arguments[i] = EvaluateNode(call.Arguments[i], context);
		}

		try
		{
			return function.Invoke(arguments);
		}
		catch (ScriptRuntimeException)
		{
			throw;
		}
		catch (Exception ex)
		{
			throw new ScriptRuntimeException($"函数 {call.Name} 执行失败：{ex.Message}");
		}
	}

	private static string DescribeArity(ScriptFunction function)
		=> function.MinArguments == function.MaxArguments
			? function.MinArguments.ToString(System.Globalization.CultureInfo.InvariantCulture)
			: $"{function.MinArguments}~{function.MaxArguments}";

	private static double RequireNumber(ScriptValue value, string operatorName)
		=> value.IsNumber
			? value.AsNumber()
			: throw new ScriptRuntimeException($"运算符 {operatorName} 只接受数值，实际是 {value.Type}");

	private static bool RequireBool(ScriptValue value, string operatorName)
		=> value.IsBool
			? value.AsBool()
			: throw new ScriptRuntimeException(
				$"运算符 {operatorName} 只接受布尔，实际是 {value.Type}（请显式写成 x != 0 之类的判断）");

	private static ScriptValue Divide(ScriptValue left, ScriptValue right)
	{
		double divisor = RequireNumber(right, "/");
		if (divisor == 0d)
		{
			throw new ScriptRuntimeException("除数为 0（不返回 Infinity，避免污染后续计算）");
		}

		return ScriptValue.FromNumber(RequireNumber(left, "/") / divisor);
	}

	private static ScriptValue Modulo(ScriptValue left, ScriptValue right)
	{
		double divisor = RequireNumber(right, "%");
		if (divisor == 0d)
		{
			throw new ScriptRuntimeException("取余的除数为 0");
		}

		return ScriptValue.FromNumber(RequireNumber(left, "%") % divisor);
	}

	private static ScriptValue Comparison(ScriptValue left, ScriptValue right, string operatorName, bool negate)
	{
		if (left.Type != right.Type)
		{
			throw new ScriptRuntimeException(
				$"运算符 {operatorName} 两侧类型必须相同，实际是 {left.Type} 与 {right.Type}");
		}

		bool equal = left.Equals(right);
		return ScriptValue.FromBool(negate ? !equal : equal);
	}
}
