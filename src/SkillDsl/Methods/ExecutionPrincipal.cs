namespace SkillDsl.Methods;

/// <summary>
/// 调用方身份。<b>授权矩阵在这一层硬编码，不经数据配置</b>（可通过派生类扩展，但默认五个身份是固定的）。
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>System</b>：全部 7 项能力。</item>
/// <item><b>WorldAgent</b>：WorldState / Narrative / Economy / NpcMind。</item>
/// <item><b>NpcAgent</b>：NpcMind / Narrative / Movement。<b>刻意不含 Combat 与 Economy</b>——
/// NPC 想造成伤害只能走 Intent → 裁决 → UnitCommand → 战斗状态机。</item>
/// <item><b>Rule</b>：Combat / Movement / WorldState / Narrative / Economy。</item>
/// <item><b>Debug</b>：持有全部能力，但<b>只允许 <see cref="MethodSideEffects.None"/> 的方法</b>——
/// 也就是"只读的 System"。开发控制台要能查世界旗标，但不能改。生产构建不暴露这个身份。</item>
/// </list>
/// </remarks>
public sealed class ExecutionPrincipal
{
	private const MethodCapability AllCapabilities =
		MethodCapability.Combat | MethodCapability.Movement | MethodCapability.WorldState
		| MethodCapability.Narrative | MethodCapability.Economy | MethodCapability.NpcMind
		| MethodCapability.Meta;

	private ExecutionPrincipal(PrincipalKind kind, string name, MethodCapability granted)
	{
		Kind = kind;
		Name = name;
		GrantedCapabilities = granted;
	}

	/// <summary>身份类别。</summary>
	public PrincipalKind Kind { get; }

	/// <summary>身份显示名，用于日志与审计（如 <c>NpcAgent#npc_qingxi_chenjiujin</c>）。</summary>
	public string Name { get; }

	/// <summary>持有的能力集合。</summary>
	public MethodCapability GrantedCapabilities { get; }

	/// <summary>引擎内部身份，拥有全部能力。</summary>
	public static ExecutionPrincipal System(string name = "System")
		=> new(PrincipalKind.System, name, AllCapabilities);

	/// <summary>世界导演身份（L3）。</summary>
	public static ExecutionPrincipal WorldAgent(string name)
		=> new(PrincipalKind.WorldAgent, name,
			MethodCapability.WorldState | MethodCapability.Narrative
			| MethodCapability.Economy | MethodCapability.NpcMind);

	/// <summary>NPC 心智身份（L2）。<b>不含 Combat / Economy</b>。</summary>
	public static ExecutionPrincipal NpcAgent(string name)
		=> new(PrincipalKind.NpcAgent, name,
			MethodCapability.NpcMind | MethodCapability.Narrative | MethodCapability.Movement);

	/// <summary>策划数据身份（技能 / 绑定）。</summary>
	public static ExecutionPrincipal Rule(string name = "Rule")
		=> new(PrincipalKind.Rule, name,
			MethodCapability.Combat | MethodCapability.Movement | MethodCapability.WorldState
			| MethodCapability.Narrative | MethodCapability.Economy);

	/// <summary>玩家输入身份。权限与 <see cref="Rule"/> 相同，仅用于审计区分。</summary>
	public static ExecutionPrincipal Player(string name = "Player")
		=> new(PrincipalKind.Player, name,
			MethodCapability.Combat | MethodCapability.Movement | MethodCapability.WorldState
			| MethodCapability.Narrative | MethodCapability.Economy);

	/// <summary>开发控制台身份：<b>只读的 System</b>——能力齐全，但只能调无副作用的方法。</summary>
	public static ExecutionPrincipal Debug(string name = "Debug")
		=> new(PrincipalKind.Debug, name, AllCapabilities);

	/// <summary>
	/// 模型提议的意图身份：<b>能力与 <see cref="Rule"/> 相同，仅用于审计区分</b>。
	/// </summary>
	/// <param name="name">显示名（如 <c>npc_qingxi_chenjiujin</c>）。</param>
	/// <returns>身份。</returns>
/// <remarks>
	/// <para><b>为什么不直接给 <see cref="NpcAgent"/></b>：那个身份刻意不含 Combat
	/// （NPC 想造成伤害必须走 Intent → 裁决 → 指令 → 战斗状态机）。
	/// 走到这里时裁决<b>已经过了</b>，再用一个拿不到能力集的身份去执行，
	/// 会把“已经批准的事”在执行层再拒一次——两套真相。</para>
/// </remarks>
	public static ExecutionPrincipal Llm(string name)
		=> new(PrincipalKind.Llm, name,
			MethodCapability.Combat | MethodCapability.Movement | MethodCapability.WorldState
			| MethodCapability.Narrative | MethodCapability.Economy);

	/// <summary>判定该身份是否有权调用某个方法。判定顺序：先只读限制，再看能力。</summary>
	public bool CanInvoke(MethodSpec method)
	{
		ArgumentNullException.ThrowIfNull(method);

		// 1) 只读身份不得调用任何有副作用的方法。
		if (Kind == PrincipalKind.Debug && method.SideEffects != MethodSideEffects.None)
		{
			return false;
		}

		// 2) 未声明能力要求的方法（纯查询）对所有身份开放。
		if (method.RequiredCapability == MethodCapability.None)
		{
			return true;
		}

		// 3) 能力集必须覆盖方法要求（方法侧保证是单个位，这里用"覆盖"语义以便未来支持多能力）。
		return (GrantedCapabilities & method.RequiredCapability) == method.RequiredCapability;
	}

	/// <inheritdoc />
	public override string ToString() => $"{Kind}#{Name}";
}
