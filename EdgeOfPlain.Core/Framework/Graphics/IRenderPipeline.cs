namespace EdgeOfPlain.Core.Framework.Graphics;

/// <summary>
/// 一次绘制批次的边界：<see cref="Begin"/> 之后场景提交绘制，<see cref="End"/> 时统一排序输出。
/// 场景在 Core 里只能通过它描述"画什么"，由宿主翻译成具体图形 API。
/// </summary>
public interface IRenderPipeline
{
	void Begin();

	/// <summary>提交一次绘制，坐标为左上角位置。</summary>
	void Draw(RenderMetadata metadata, float x, float y);

	void End();
}
