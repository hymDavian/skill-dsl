namespace SkillDsl.Methods;

/// <summary>
/// 方法的副作用标记。用于"只读身份"与审计，不参与能力授权计算。
/// </summary>
[Flags]
public enum MethodSideEffects
{
	/// <summary>纯查询：不改变任何状态。可被开发控制台等只读身份调用。</summary>
	None = 0,

	/// <summary>修改游戏状态（改属性、改关系、改物品归属）。</summary>
	StateChange = 1 << 0,

	/// <summary>发布事件，可能引发链式绑定（需配合深度限制与预算）。</summary>
	EventPublish = 1 << 1,

	/// <summary>创建实体（刷怪、生成物品、召唤）。最易被滥用，单独标记。</summary>
	Spawn = 1 << 2,
}
