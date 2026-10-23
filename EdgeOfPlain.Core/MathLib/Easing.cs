namespace EdgeOfPlain.Core.MathLib;

public static class Easing
{
	public static float OutQuad(float t) => 1 - (1 - t) * (1 - t);
	public static float InQuad(float t) => t * t;
	public static float OutInQuad(float t)
	{
		if (t < 0.5f)
			return 2f * t * (1f - t);

		var x = 2f * t - 1f;
		return 0.5f + 0.5f * x * x;
	}

	public static float OutBack(float t, float s = 1.70158f)
	{
		t -= 1;
		return 1 + t * t * ((s + 1) * t + s);
	}
}