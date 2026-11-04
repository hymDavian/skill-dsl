namespace SkillDsl.Methods;

/// <summary>
/// 方法注册表：<c>Id → MethodSpec</c>。
/// </summary>
/// <remarks>
/// 注册阶段把所有"写错了"的情况挡在启动时（Id 非法、重复、能力声明多于一bit），
/// 而不是等运行到那一行才炸。
/// </remarks>
public sealed class MethodRegistry
{
	private readonly Dictionary<string, MethodSpec> _methods = new(StringComparer.Ordinal);

	/// <summary>已注册的方法数量。</summary>
	public int Count => _methods.Count;

	/// <summary>全部已注册方法。</summary>
	public IReadOnlyCollection<MethodSpec> All => _methods.Values;

	/// <summary>注册一个方法。</summary>
	/// <exception cref="ArgumentException">Id 非法、参数声明非法、能力声明不是单个位。</exception>
	/// <exception cref="InvalidOperationException">Id 重复。</exception>
	public void Register(MethodSpec spec)
	{
		ArgumentNullException.ThrowIfNull(spec);

		ValidateId(spec.Id);
		ValidateParameters(spec);
		ValidateCapability(spec);

		if (!_methods.TryAdd(spec.Id, spec))
		{
			throw new InvalidOperationException($"方法 {spec.Id} 已注册，不允许重复注册");
		}
	}

	/// <summary>按 Id 取方法。</summary>
	public bool TryGet(string id, out MethodSpec? spec)
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			spec = null;
			return false;
		}

		return _methods.TryGetValue(id, out spec);
	}

	/// <summary>
	/// 取出某身份可调用的全部方法，<b>按 Id 排序</b>。
	/// </summary>
/// <remarks>
	/// 这就是 LLM 工具表的投影来源。排序是刻意的：工具表顺序变化会让提示词缓存失效、也让回归测试不稳定。
/// </remarks>
	public IReadOnlyList<MethodSpec> GetInvocableBy(ExecutionPrincipal principal)
	{
		ArgumentNullException.ThrowIfNull(principal);

		return _methods.Values
			.Where(principal.CanInvoke)
			.OrderBy(static method => method.Id, StringComparer.Ordinal)
			.ToArray();
	}

	private static void ValidateId(string id)
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			throw new ArgumentException("方法 Id 不能为空", nameof(id));
		}

		foreach (char value in id)
		{
			if (!char.IsAsciiLetterOrDigit(value) && value != '_' && value != '.')
			{
				throw new ArgumentException($"方法 Id '{id}' 含有非法字符 '{value}'（只允许字母、数字、下划线、点）", nameof(id));
			}
		}

		if (!id.Contains('.', StringComparison.Ordinal))
		{
			throw new ArgumentException($"方法 Id '{id}' 必须形如 <域>.<动作>，例如 unit.apply_damage", nameof(id));
		}
	}

	private static void ValidateParameters(MethodSpec spec)
	{
		ArgumentNullException.ThrowIfNull(spec.Parameters);

		HashSet<string> names = new(StringComparer.Ordinal);
		bool sawOptional = false;

		foreach (ParamSpec parameter in spec.Parameters)
		{
			if (parameter is null)
			{
				throw new ArgumentException($"方法 {spec.Id} 的参数表含有 null 项", nameof(spec));
			}

			if (string.IsNullOrWhiteSpace(parameter.Name))
			{
				throw new ArgumentException($"方法 {spec.Id} 存在没有名字的参数", nameof(spec));
			}

			if (!names.Add(parameter.Name))
			{
				throw new ArgumentException($"方法 {spec.Id} 的参数名 '{parameter.Name}' 重复", nameof(spec));
			}

			// 参数按位置绑定，跳过可选参数去传必填参数是不可能的。
			// 这种声明不会报错，但永远无法被正确调用（总是 E3212），必须在注册期就拒掉。
			if (parameter.IsRequired && sawOptional)
			{
				throw new ArgumentException(
					$"方法 {spec.Id} 的必填参数 '{parameter.Name}' 跟在可选参数之后，将永远无法被传入。"
					+ "请把它前置，或给它一个缺省值。",
					nameof(spec));
			}

			sawOptional |= !parameter.IsRequired;
		}
	}

	private static void ValidateCapability(MethodSpec spec)
	{
		int bits = (int)spec.RequiredCapability;

		// 允许 0（无需能力）或单个位；多个位会让"需要 A 或 B"与"需要 A 且 B"产生歧义。
		if (bits != 0 && (bits & (bits - 1)) != 0)
		{
			throw new ArgumentException(
				$"方法 {spec.Id} 的 RequiredCapability 必须是单个能力位，实际是 {spec.RequiredCapability}。"
				+ "若一个方法在不同场景需要不同权限，请拆成两个方法。",
				nameof(spec));
		}
	}
}
