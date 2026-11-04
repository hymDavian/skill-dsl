namespace SkillDsl.Methods;

/// <summary>方法派发的错误码。禁止在业务代码里裸写错误码字符串。</summary>
public static class MethodErrorCodes
{
	/// <summary>方法未注册。</summary>
	public const string UnknownMethod = "E3201";

	/// <summary>实参个数超过声明个数。</summary>
	public const string TooManyArguments = "E3202";

	/// <summary>表达式实参求值失败。</summary>
	public const string ArgumentEvaluationFailed = "E3203";

	/// <summary>身份无权调用该方法。</summary>
	public const string CapabilityDenied = "E3210";

	/// <summary>实参类型与声明不符。</summary>
	public const string ArgumentTypeMismatch = "E3211";

	/// <summary>缺少必填参数。</summary>
	public const string MissingRequiredArgument = "E3212";

	/// <summary>方法实现抛异常，或返回值类型与声明不符。</summary>
	public const string InvocationFailed = "E3213";
}
