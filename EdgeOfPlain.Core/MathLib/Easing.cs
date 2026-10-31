using FixedMathSharp;

namespace EdgeOfPlain.Core.MathLib;

public static class Easing
{
	public static readonly Fixed64 DefaultOutBackOvershoot = (Fixed64)1.70158;

	public static Fixed64 OutQuad(Fixed64 t) => Fixed64.One - (Fixed64.One - t) * (Fixed64.One - t);
	public static Fixed64 InQuad(Fixed64 t) => t * t;

	public static Fixed64 OutInQuad(Fixed64 t)
	{
		if (t < (Fixed64)0.5)
			return (Fixed64)2 * t * (Fixed64.One - t);

		var x = (Fixed64)2 * t - Fixed64.One;
		return Fixed64.Half + Fixed64.Half * x * x;
	}

	public static Fixed64 OutBack(Fixed64 t) => OutBack(t, DefaultOutBackOvershoot);

	public static Fixed64 OutBack(Fixed64 t, Fixed64 s)
	{
		t -= Fixed64.One;
		return Fixed64.One + t * t * ((s + Fixed64.One) * t + s);
	}
}
