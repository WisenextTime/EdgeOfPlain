using System;
using EdgeOfPlain.Scr.Core;
using EdgeOfPlain.Scr.Core.Global;
using Godot;
using static EdgeOfPlain.Scr.Core.Global.Global;
using EdgeOfPlain.Scr.Core.Lib;
using EdgeOfPlain.Scr.Core.Resources;

namespace EdgeOfPlain.Scr.Debug;
public partial class TileDebug : ItemList
{
	private Label _description;
	private Sprite2D _sprite;
	private PointLight2D _light;

	public override void _Ready()
	{
		_description = GetNode<Label>("Label");
		_sprite = GetNode<Sprite2D>("Preview/Image");
		_light = GetNode<PointLight2D>("Preview/Image/Light");
		Launcher.Launch();
		foreach (var tile in Instance.Tiles)
		{
			AddItem(tile.Value.Id, tile.Value.Texture);
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsAnythingSelected()) return;
		var nowSelected = Instance.Tiles[GetItemText(GetSelectedItems()[0])];
		_description.Text = $"{nowSelected.Id}\n" + 
		                    $"Roughness : {nowSelected.Rough}" + 
		                    $"       Humidity : {nowSelected.Humidity}\n" +
		                    $"Temperature : {nowSelected.Temperature}" +
		                    $"       Size : {GD.VarToStr(nowSelected.Size)}\n" +
		                    $"Lighted : {nowSelected.CanLighted}\n" +
		                    (nowSelected.CanLighted
			                    ? $"Color : {nowSelected.LightColor}" +
			                      $"     Intensity : {Tile.LightIntensity}"
			                    : "");
		_sprite.Texture = Instance.Tiles[nowSelected.Id].Texture;
		_light.Enabled = nowSelected.CanLighted;
		_light.Texture = Instance.Tiles[nowSelected.Id].LightTexture;
		_light.Color = Instance.Tiles[nowSelected.Id].LightColor;
		_light.Energy = Tile.LightIntensity;
		GetNode<Node2D>("GlobalLight").Visible = nowSelected.CanLighted;
	}

}
