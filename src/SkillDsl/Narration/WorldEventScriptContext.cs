using SkillDsl;

namespace SkillDsl.Narration;

/// <summary>
/// 世界事件的表达式上下文：只提供 <c>event_*</c> 几个字段，外加叙述通道。
/// </summary>
/// <remarks>
/// <para><b>它没有 <c>caster.*</c> / <c>target.*</c></b>：世界事件发生时没有"谁在打谁"。
/// 绑定里写 <c>caster.hp</c> 会读不到——那是刻意的，静默返回 0 会让条件永远成立。</para>
/// <para><b>不绑定具体事件类型</b>：只收名字、强度、时刻三样东西。
/// 这样日边界、离线演化、将来的天灾都能复用同一个上下文，
/// 而不必为每种事件写一个 <c>XxxScriptContext</c>。</para>
/// <para><b>命名与数据一致</b>：<c>event_severity</c> 用的就是 <c>bnd_*.json</c> 里 <c>when</c> 写的那个名字。
/// 加字段时两边一起改，否则条件会变成“永远读不到 → 永远不触发”，而这没有任何报错。</para>
/// </remarks>
public sealed class WorldEventScriptContext : IScriptContext
{
	/// <summary>创建上下文。</summary>
	/// <param name="name">数据侧的完整事件名（例如 <c>world_event.day_started</c>）。</param>
	/// <param name="severity">强度（0–1）。</param>
	/// <param name="gameSeconds">事件发生时的游戏时间（秒）。</param>
	/// <param name="narration">叙述通道；为 null 时 <c>narr.*</c> 会报未装配。</param>
	/// <param name="variables">
	/// 额外变量（如 <c>quest_talk_target</c>）；为 null 时只有 <c>event_*</c> 三个。
	/// </param>
	/// <exception cref="ArgumentException"><paramref name="name"/> 为空。</exception>
	public WorldEventScriptContext(
		string name,
		double severity,
		double gameSeconds,
		INarrativeGateway? narration = null,
		IReadOnlyDictionary<string, ScriptValue>? variables = null)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);

		Name = name;
		Severity = severity;
		GameSeconds = gameSeconds;
		Narration = narration;
		_variables = variables;
	}

	private readonly IReadOnlyDictionary<string, ScriptValue>? _variables;

	/// <summary>事件名。</summary>
	public string Name { get; }

	/// <summary>强度（0–1）。</summary>
	public double Severity { get; }

	/// <summary>发生时的游戏时间（秒）。</summary>
	public double GameSeconds { get; }

	/// <inheritdoc />
	public INarrativeGateway? Narration { get; }

	/// <inheritdoc />
	public bool TryGetVariable(string path, out ScriptValue value)
	{
		switch (path)
		{
			case "event_severity":
				value = ScriptValue.FromNumber(Severity);
				return true;

			case "event_name":
				value = ScriptValue.FromString(Name);
				return true;

			case "event_time":
				value = ScriptValue.FromNumber(GameSeconds);
				return true;

			default:
				// 额外变量（委托目标之类）由注册方提供，见 WorldEventRegistry
				if (_variables is not null && _variables.TryGetValue(path, out ScriptValue extra))
				{
					value = extra;
					return true;
				}

				value = ScriptValue.Null;
				return false;
		}
	}
}
