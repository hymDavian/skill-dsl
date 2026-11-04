namespace LivingWorld.Core.Scripting.Methods;

/// <summary>
/// 一个可被数据 / LLM 调用的原子方法。
/// </summary>
/// <remarks>
/// 方法<b>必须</b>：同步、有界耗时、无 IO、无网络、无时钟、无随机（随机走注入的种子化服务）。
/// 违反会让存档回放与决策记录（见 12 章）失效。
/// </remarks>
public sealed class MethodSpec
{
	/// <summary>
	/// 方法 Id，全局唯一，形如 <c>unit.apply_damage</c>。
	/// 一旦被数据引用即成为契约，重命名必须提供数据迁移。
	/// </summary>
	public required string Id { get; init; }

	/// <summary>给人看的名字（编辑器面板、错误信息）。</summary>
	public required string DisplayName { get; init; }

	/// <summary>参数声明（按顺序）。</summary>
	public required IReadOnlyList<ParamSpec> Parameters { get; init; }

	/// <summary>返回类型；<c>null</c> 表示无返回值。</summary>
	public ParamType? ReturnType { get; init; }

	/// <summary>所需能力。<b>必须是单个位或 <see cref="MethodCapability.None"/></b>，注册时校验。</summary>
	public MethodCapability RequiredCapability { get; init; }

	/// <summary>副作用标记。</summary>
	public MethodSideEffects SideEffects { get; init; }

	/// <summary>说明。会原样进入 LLM 工具表，所以要写清"做什么"与"什么时候不该用"。</summary>
	public required string Description { get; init; }

	/// <summary>
	/// 实现。参数已求值并校验过个数与类型；无返回值时返回 <see cref="ScriptValue.Null"/>。
	/// </summary>
/// <remarks>
	/// <b>为什么实现要拿到 <see cref="IScriptContext"/></b>：像 <c>unit.apply_damage</c> 这种方法需要知道
	/// "打谁"，而目标是<b>执行上下文</b>的一部分，不是参数——否则只能靠 <c>$ref</c> 把单位引用塞进
	/// 表达式内核，而内核刻意只支持四种值。让实现自己去读上下文，比把引用变成第五种值干净得多。
/// </remarks>
	public required Func<IReadOnlyList<ScriptValue>, IScriptContext, ScriptValue> Invoke { get; init; }
}
