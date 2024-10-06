using EdgeOfPlain.Scr.Core.Lib;
using Godot;
using static EdgeOfPlain.Scr.Core.Global.Global;

namespace EdgeOfPlain.Scr.Core.Resources
{
	public class Tile(string id)
	{
		public string Id { get; } = id;
		public Vector2I Size { get; set; } = new(32, 32);
		public TileMoveType TileMoveType { get; set; } = TileMoveType.None;
		public ImageType ImageType { get; set; } = ImageType.Default;
		public string ImageName { get; set; } = IndexImage.Default;

		public Texture2D Texture => ImageLoader.LoadImage(ImageName, "Tile", Id);

		public float Rough = 1f;
		//粗糙度决定单位在该地块上行走的加速度减速度修正,数值越低越光滑
		//注意,粗糙度应该是一个不为零的正浮点数
		
		public float Temperature = 5f;
		public float Humidity = 5f;
		//温度和潮湿度作为随机地图生成地块的判定
		//TODO
		
		//Light 只能提供荧光贴图
		public bool CanLighted = false;
		public string LightImageName { get; set; } = IndexImage.Default;
		public Texture2D LightTexture =>ImageLoader.LoadImage(ImageName, "Tile", Id, "_light");
		public const float LightIntensity = 1f;
		public Color LightColor = Color.Color8(255, 255, 255);

		public static void NewTile(Tile tile)
		{
			Instance.Tiles.Add(tile.Id, tile);
		}
	}

	public class GroundTile : Tile
	{
		public GroundTile(string id) : base(id)
		{
			TileMoveType = TileMoveType.Ground;
		}
	}
	
	public class WaterTile : Tile
	{
		public WaterTile(string id) : base(id)
		{
			TileMoveType = TileMoveType.Water;
			Humidity = 8f;
		}
	}
}