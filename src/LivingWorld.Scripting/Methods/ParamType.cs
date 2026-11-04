namespace LivingWorld.Core.Scripting.Methods;

/// <summary>方法参数的类型。仅用于参数表声明，与表达式内核的 <see cref="ScriptValueType"/> 是两套东西。</summary>
/// <remarks>
/// 分开的理由：将来要加 <c>UnitRef</c>、<c>Vec3</c> 这类"方法参数专属"类型，
/// 它们不该进表达式内核（那是批次 1 定下的边界）。两者通过转换表连接，而不是合并成一个枚举。
/// </remarks>
public enum ParamType
{
	/// <summary>数值。</summary>
	Number = 0,

	/// <summary>布尔。</summary>
	Bool = 1,

	/// <summary>字符串。</summary>
	String = 2,
}
