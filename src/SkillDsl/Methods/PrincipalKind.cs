namespace SkillDsl.Methods;

/// <summary>调用方的身份类别。</summary>
public enum PrincipalKind
{
	/// <summary>引擎内部：tick、存档、世界模拟。拥有全部能力。</summary>
	System = 0,

	/// <summary>L3 世界 Agent（守卷人）。能导演世界，不能直接改战斗数值。</summary>
	WorldAgent = 1,

	/// <summary>L2 NPC 的 LLM 意图。<b>刻意不含 Combat 与 Economy</b>。</summary>
	NpcAgent = 2,

	/// <summary>策划编写并校验过的数据（技能 / 绑定）。可以写完整玩法。</summary>
	Rule = 3,

	/// <summary>
	/// 玩家输入：点地移动、按键施法。
	/// </summary>
	/// <remarks>权限与 <see cref="Rule"/> 相同（技能数据是可信的），差别只在<b>身份可审计</b>：
	/// 日志里能分清“这条伤害是玩家打的还是世界规则触发的”。</remarks>
	Player = 4,

	/// <summary>开发控制台：持有全部能力，但只允许无副作用的方法。</summary>
	Debug = 5,

	/// <summary>
	/// 模型提议的意图（经历裁决、变成指令之后）。
	/// </summary>
/// <remarks>
	/// <b>能力集与 <see cref="Rule"/> 相同</b>，差别只在身份可审计：
	/// 日志里能分清“这条伤害是玩家打的、规则算的、还是模型提的”。
	/// 不用 <see cref="NpcAgent"/> 是因为那个身份<b>刻意没有 Combat</b>，
	/// 而模型提议的 <c>cast_skill</c> 一旦裁决通过就必须能真的施放。
/// </remarks>
	Llm = 6,
}
