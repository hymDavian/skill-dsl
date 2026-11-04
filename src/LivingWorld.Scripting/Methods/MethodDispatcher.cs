namespace LivingWorld.Core.Scripting.Methods;

/// <summary>
/// 方法派发器：把一个"方法 Id + 实参列表"变成一次真实调用。
/// </summary>
/// <remarks>
/// <para>固定执行顺序（顺序本身就是安全保证）：</para>
/// <list type="number">
/// <item>方法存在性检查</item>
/// <item><b>授权检查</b>——必须在任何副作用之前</item>
/// <item>实参个数检查</item>
/// <item>逐个求值 + 类型检查 + 填缺省值</item>
/// <item>调用实现 + 返回类型检查</item>
/// </list>
/// </remarks>
public sealed class MethodDispatcher
{
	private readonly MethodRegistry _registry;
	private readonly ExpressionEvaluator _evaluator;

	/// <summary>用注册表与函数白名单构造。</summary>
	/// <param name="registry">方法注册表。</param>
	/// <param name="functions">表达式函数白名单（供 <see cref="MethodArgument.Computed"/> 求值）。</param>
	public MethodDispatcher(MethodRegistry registry, ScriptFunctionTable functions)
	{
		ArgumentNullException.ThrowIfNull(registry);
		ArgumentNullException.ThrowIfNull(functions);

		_registry = registry;
		_evaluator = new ExpressionEvaluator(functions);
	}

	/// <summary>派发一次调用。</summary>
	/// <param name="methodId">方法 Id。</param>
	/// <param name="arguments">实参列表；少于声明个数时用缺省值补齐。</param>
	/// <param name="principal">调用方身份，决定授权。</param>
	/// <param name="context">表达式求值上下文（由宿主提供变量）。</param>
	public MethodDispatchResult Dispatch(
		string methodId,
		IReadOnlyList<MethodArgument>? arguments,
		ExecutionPrincipal principal,
		IScriptContext context)
	{
		ArgumentNullException.ThrowIfNull(principal);
		ArgumentNullException.ThrowIfNull(context);

		// ① 方法存在
		if (!_registry.TryGet(methodId, out MethodSpec? method) || method is null)
		{
			return MethodDispatchResult.Failure(
				MethodErrorCodes.UnknownMethod,
				$"未注册的方法：{methodId}");
		}

		// ② 授权（在副作用之前；被拒绝的调用不留下任何痕迹）
		if (!principal.CanInvoke(method))
		{
			return MethodDispatchResult.Failure(
				MethodErrorCodes.CapabilityDenied,
				$"身份 {principal} 无权调用 {methodId}：需要能力 {method.RequiredCapability}，"
				+ $"实际持有 {principal.GrantedCapabilities}，方法副作用 {method.SideEffects}");
		}

		arguments ??= Array.Empty<MethodArgument>();

		// ③ 个数
		if (arguments.Count > method.Parameters.Count)
		{
			return MethodDispatchResult.Failure(
				MethodErrorCodes.TooManyArguments,
				$"方法 {methodId} 只接受 {method.Parameters.Count} 个参数，实际传入 {arguments.Count} 个");
		}

		// ④ 逐个绑定
		ScriptValue[] bound = new ScriptValue[method.Parameters.Count];

		for (int index = 0; index < method.Parameters.Count; index++)
		{
			ParamSpec spec = method.Parameters[index];
			MethodArgument? supplied = index < arguments.Count ? arguments[index] : null;

			ScriptValue raw;

			if (supplied is null)
			{
				if (spec.IsRequired)
				{
					return MethodDispatchResult.Failure(
						MethodErrorCodes.MissingRequiredArgument,
						$"方法 {methodId} 缺少必填参数 '{spec.Name}'");
				}

				raw = spec.Default;
			}
			else
			{
				switch (supplied)
				{
					case MethodArgument.Constant constant:
						raw = constant.Value;
						break;

					case MethodArgument.Computed computed:
						try
						{
							raw = _evaluator.Evaluate(computed.Expression, context);
						}
						catch (ScriptRuntimeException ex)
						{
							return MethodDispatchResult.Failure(
								MethodErrorCodes.ArgumentEvaluationFailed,
								$"方法 {methodId} 的参数 '{spec.Name}' 求值失败：{ex.Message}");
						}

						break;

					default:
						return MethodDispatchResult.Failure(
							MethodErrorCodes.ArgumentTypeMismatch,
							$"方法 {methodId} 的参数 '{spec.Name}' 出现了未支持的实参节点");
				}
			}

			if (!MatchesType(raw, spec.Type))
			{
				return MethodDispatchResult.Failure(
					MethodErrorCodes.ArgumentTypeMismatch,
					$"方法 {methodId} 的参数 '{spec.Name}' 需要 {spec.Type}，实际是 {raw.Type}（本版本不做隐式转换）");
			}

			bound[index] = raw;
		}

		// ⑤ 调用
		try
		{
			ScriptValue result = method.Invoke(bound, context);

			if (method.ReturnType is ParamType expectedReturn && !MatchesType(result, expectedReturn))
			{
				return MethodDispatchResult.Failure(
					MethodErrorCodes.InvocationFailed,
					$"方法 {methodId} 声明返回 {expectedReturn}，实际返回 {result.Type}");
			}

			return MethodDispatchResult.Success(result);
		}
		catch (Exception ex)
		{
			return MethodDispatchResult.Failure(
				MethodErrorCodes.InvocationFailed,
				$"方法 {methodId} 执行失败：{ex.Message}");
		}
	}

	/// <summary>
	/// 类型匹配检查。<b>本版本不做任何隐式转换</b>——数字不会自动变字符串，反之亦然。
	/// </summary>
	private static bool MatchesType(ScriptValue value, ParamType type)
		=> type switch
		{
			ParamType.Number => value.IsNumber,
			ParamType.Bool => value.IsBool,
			ParamType.String => value.IsString,
			_ => false,
		};
}
