namespace SkillDsl;

/// <summary>一个可在 DSL 中调用的函数。</summary>
public sealed class ScriptFunction
{
	/// <summary>函数名（DSL 中书写的形式，区分大小写）。</summary>
	public required string Name { get; init; }

	/// <summary>最少参数个数。</summary>
	public required int MinArguments { get; init; }

	/// <summary>最多参数个数。</summary>
	public required int MaxArguments { get; init; }

	/// <summary>说明，用于编辑器提示与 LLM 工具表。</summary>
	public required string Description { get; init; }

	/// <summary>实现。参数已由求值器求值并校验个数。</summary>
	public required Func<IReadOnlyList<ScriptValue>, ScriptValue> Invoke { get; init; }
}

/// <summary>
/// 函数白名单。
/// </summary>
/// <remarks>
/// DSL 里<b>不存在</b>"任意函数调用"：名字不在本表内就是错误。
/// 这是表达式层的第一道安全边界——与后续方法注册表的权限分级共同构成"能做什么"的全部范围。
/// </remarks>
public sealed class ScriptFunctionTable
{
	private readonly Dictionary<string, ScriptFunction> _functions = new(StringComparer.Ordinal);

	/// <summary>已登记的全部函数。</summary>
	public IReadOnlyCollection<ScriptFunction> All => _functions.Values;

	/// <summary>登记一个函数；同名重复登记会抛异常（避免静默覆盖）。</summary>
	public void Register(ScriptFunction function)
	{
		ArgumentNullException.ThrowIfNull(function);

		if (string.IsNullOrWhiteSpace(function.Name))
		{
			throw new ArgumentException("函数名不能为空", nameof(function));
		}

		if (function.MinArguments < 0 || function.MaxArguments < function.MinArguments)
		{
			throw new ArgumentException($"函数 {function.Name} 的参数个数区间非法", nameof(function));
		}

		if (!_functions.TryAdd(function.Name, function))
		{
			throw new InvalidOperationException($"函数 {function.Name} 已登记，不允许重复登记");
		}
	}

	/// <summary>按名字取函数。</summary>
	public bool TryGet(string name, out ScriptFunction? function)
		=> _functions.TryGetValue(name, out function);

	/// <summary>
	/// 建立默认函数表（纯数学，无副作用、无随机、无 IO）。
	/// </summary>
/// <remarks>
	/// 刻意不含随机数：表达式必须<b>确定性</b>，随机只能来自宿主注入的种子化服务（后续批次）。
/// </remarks>
	public static ScriptFunctionTable CreateDefault()
	{
		ScriptFunctionTable table = new();

		table.Register(Unary("abs", "取绝对值", Math.Abs));
		table.Register(Unary("floor", "向下取整", Math.Floor));
		table.Register(Unary("ceil", "向上取整", Math.Ceiling));
		table.Register(Unary("round", "四舍五入（.5 远离零，避免银行家舍入的意外）", value => Math.Round(value, MidpointRounding.AwayFromZero)));
		table.Register(Unary("sqrt", "平方根；负数报错", Math.Sqrt));

		table.Register(Binary("min", "取较小值", Math.Min));
		table.Register(Binary("max", "取较大值", Math.Max));

		table.Register(new ScriptFunction
		{
			Name = "clamp",
			MinArguments = 3,
			MaxArguments = 3,
			Description = "把值限制在 [最小值, 最大值] 区间内",
			Invoke = args =>
			{
				double value = args[0].AsNumber();
				double min = args[1].AsNumber();
				double max = args[2].AsNumber();

				if (min > max)
				{
					throw new ScriptRuntimeException($"clamp 的最小值({min})大于最大值({max})");
				}

				return ScriptValue.FromNumber(Math.Clamp(value, min, max));
			},
		});

		return table;
	}

	private static ScriptFunction Unary(string name, string description, Func<double, double> body)
		=> new()
		{
			Name = name,
			MinArguments = 1,
			MaxArguments = 1,
			Description = description,
			Invoke = args => ScriptValue.FromNumber(body(args[0].AsNumber())),
		};

	private static ScriptFunction Binary(string name, string description, Func<double, double, double> body)
		=> new()
		{
			Name = name,
			MinArguments = 2,
			MaxArguments = 2,
			Description = description,
			Invoke = args => ScriptValue.FromNumber(body(args[0].AsNumber(), args[1].AsNumber())),
		};
}
