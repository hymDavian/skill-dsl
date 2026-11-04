namespace LivingWorld.Core.Scripting.Methods;

/// <summary>一个方法参数的声明。</summary>
public sealed class ParamSpec
{
	/// <summary>参数名（用于错误信息与 LLM 工具表）。</summary>
	public required string Name { get; init; }

	/// <summary>参数类型。</summary>
	public required ParamType Type { get; init; }

	/// <summary>缺省值。为 <see cref="ScriptValue.Null"/> 表示该参数必填。</summary>
	public ScriptValue Default { get; init; }

	/// <summary>是否必填（即没有缺省值）。</summary>
	public bool IsRequired => Default.IsNull;

	/// <summary>说明，进入 LLM 工具表。</summary>
	public string? Description { get; init; }
}
