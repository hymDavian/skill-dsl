namespace SkillDsl.Methods;

/// <summary>
/// 方法能力标签。<b>位标志</b>：方法声明"需要哪些能力"，身份声明"拥有哪些能力"。
/// </summary>
/// <remarks>
/// <para>为什么用能力而不是等级：绑定（策划写的世界规则）必须能改世界旗标，
/// 而 LLM 世界 Agent 也需要——线性等级无法表达"不同来源拥有不同能力"。</para>
/// <para>方法侧要求<b>单个位</b>（或 <see cref="None"/>），注册时校验；
/// 身份侧是多个位的组合。</para>
/// </remarks>
[Flags]
public enum MethodCapability
{
	/// <summary>无需特殊能力（纯查询方法用这个）。</summary>
	None = 0,

	/// <summary>战斗：伤害、治疗、Buff、技能、状态机。</summary>
	Combat = 1 << 0,

	/// <summary>移动：位移、寻路、朝向、驻守。</summary>
	Movement = 1 << 1,

	/// <summary>世界状态：世界旗标、刷怪、天气、时间流速。</summary>
	WorldState = 1 << 2,

	/// <summary>叙事：对白、旁白、演出、谣言、提示。</summary>
	Narrative = 1 << 3,

	/// <summary>经济：物品、所有权、货币、价格。</summary>
	Economy = 1 << 4,

	/// <summary>NPC 心智：记忆、关系、目标、秘密。</summary>
	NpcMind = 1 << 5,

	/// <summary>元信息：存档、版本、全服参数。只有引擎可持有。</summary>
	Meta = 1 << 6,
}
