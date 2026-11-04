namespace LivingWorld.Core.Scripting;

/// <summary>
/// 表达式求值时向宿主索取变量的接口。
/// </summary>
/// <remarks>
/// <para>表达式内核不认识任何具体变量（<c>caster</c>、<c>skill.level</c> 都只是字符串路径）。
/// <b>能读到什么完全由宿主决定</b>——这既是防越权的边界，也让同一份 DSL 能在客户端回声与服务端权威两端运行。</para>
/// <para><b>叙述通道放在基接口上</b>（带默认实现）：世界事件、机关、绑定都可能要说话，
/// 而它们没有单位上下文。挂在单位上下文上就只有“单位效果”能说话。
/// 默认 <c>null</c> 让既有上下文实现一行不改（没装配时 <c>narr.show_toast</c> 会报未装配）。</para>
/// </remarks>
public interface IScriptContext
{
	/// <summary>按点分路径取值（如 <c>caster.Str</c>）。取不到返回 false，值为 <see cref="ScriptValue.Null"/>。</summary>
	bool TryGetVariable(string path, out ScriptValue value);

	/// <summary>叙述通道（投待播提示）；未装配时为 null。</summary>
	Narration.INarrativeGateway? Narration => null;
}
