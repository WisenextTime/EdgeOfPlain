using System;
using System.Collections.Generic;
using EdgeOfPlain.Scr.Core.Resources;
using Godot;
using static EdgeOfPlain.Scr.Core.Global.Global;

namespace EdgeOfPlain.Scr.Core.Sences;

public partial class Game : Control
{
	private Dictionary<string, int> _tileIndex = [];
	public GameTileMap TileMap;
	
	private Camera2D _camera;
	private TileMapLayer _tiles;
	
	private Navigation _navigation;
	
	public override void _Ready()
	{
		TileMap = MapParser.Load(Instance.GameMapPath);
		_tiles = GetNode<TileMapLayer>("Tiles");
		_camera = GetNode<Camera2D>("Camera");
		_navigation = GetNode<Navigation>("Navigation");
		_camera.GlobalPosition = new Vector2(TileMap.MapSize.X * 16, TileMap.MapSize.Y * 16);
		_getTiles();
		_setTiles();
		_navigation.GetTileCost(TileMap);
	}

	private void _getTiles()
	{
		{
			_tiles.Clear();
			var tileSet = new TileSet();
			{
				//0000 0000
				//AGWA WLND
				//None
				tileSet.AddPhysicsLayer(0);
				tileSet.SetPhysicsLayerCollisionLayer(0, 0b_0000_0000);
				//Water
				tileSet.AddPhysicsLayer(1);
				tileSet.SetPhysicsLayerCollisionLayer(1, 0b_0000_0100);
				//Bridge
				tileSet.AddPhysicsLayer(2);
				tileSet.SetPhysicsLayerCollisionLayer(2, 0b_0000_0000);
				//Air
				tileSet.AddPhysicsLayer(3);
				tileSet.SetPhysicsLayerCollisionLayer(3, 0b_0000_1100);
				//Void
				tileSet.AddPhysicsLayer(4);
				tileSet.SetPhysicsLayerCollisionLayer(4, 0b_0001_1100);
				//Ground
				tileSet.AddPhysicsLayer(5);
				tileSet.SetPhysicsLayerCollisionLayer(5, 0b_0000_1000);
				uint mask = 0b_0001_0000_0000;
				for (var i = 6; i < 16; i++)
				{
					tileSet.AddPhysicsLayer(i);
					tileSet.SetPhysicsLayerCollisionLayer(i, mask);
					mask <<= 1;
				}
				//Special
				tileSet.AddPhysicsLayer(16);
				tileSet.SetPhysicsLayerCollisionLayer(16, 0b_1_0000_0000_0000_0000_0000);
			}
			{
				//0000 0000 0000 0000
				//0AWG 0098 7654 3210
				//None
				tileSet.AddNavigationLayer(0);
				tileSet.SetNavigationLayerLayers(0, 0b_0111_0000_0000_0000);
				//Water
				tileSet.AddNavigationLayer(1);
				tileSet.SetNavigationLayerLayers(1, 0b_0110_0000_0000_0000);
				//Bridge
				tileSet.AddNavigationLayer(2);
				tileSet.SetNavigationLayerLayers(2, 0b_0111_0000_0000_0000);
				//Air
				tileSet.AddNavigationLayer(3);
				tileSet.SetNavigationLayerLayers(3, 0b_0100_0000_0000_0000);
				//Void
				tileSet.AddNavigationLayer(4);
				tileSet.SetNavigationLayerLayers(4, 0b_0000_0000_0000_0000);
				//Ground
				uint mask = 1;
				for (var i = 5; i < 15; i++)
				{
					tileSet.AddNavigationLayer(i);
					tileSet.SetNavigationLayerLayers(i, mask);
					mask <<= 1;
				}
			}
			{
				tileSet.TileSize = new Vector2I(32, 32);
			}
			tileSet.AddCustomDataLayer(0);
			tileSet.SetCustomDataLayerName(0,"Rough");
			tileSet.SetCustomDataLayerType(0,Variant.Type.Float);
			
			_tiles.TileSet = tileSet;
		}
		var index = 0;
		
		foreach (var tile in Instance.Tiles)
		{
			_tileIndex.Add(tile.Key, index);
			var source = new TileSetAtlasSource();
			_tiles.TileSet.AddSource(source,index);
			source.TextureRegionSize = new Vector2I(32, 32);
			source.Texture = tile.Value.Texture;
			source.CreateTile(Vector2I.Zero);
			var tileData = source.GetTileData(Vector2I.Zero, 0);
			{
				var layerId = tile.Value.TileMoveType switch
				{
					TileMoveType.Water => 1,
					TileMoveType.Bridge => 2,
					TileMoveType.Air => 3,
					TileMoveType.Void => 4,
					TileMoveType.Ground => 5,
					_ => 0
				};
				tileData.AddCollisionPolygon(layerId);
				tileData.SetCollisionPolygonPoints(layerId, 0,
				[
					new Vector2(-16, -16),
					new Vector2(16, -16),
					new Vector2(16, 16),
					new Vector2(-16, 16)
				]);

				var polygon = new NavigationPolygon();
				polygon.SetVertices([
					new Vector2(-16, -16),
					new Vector2(16, -16),
					new Vector2(16, 16),
					new Vector2(-16, 16)
				]);
				polygon.AddPolygon([0, 1, 2, 3]);
				tileData.SetNavigationPolygon(layerId, polygon);
				tileData.SetCustomData("Rough", tile.Value.Rough);

				
				if (tile.Value.CanLighted)
				{
					tileData.ZIndex = 5;
					tileData.Material = (Material)ResourceLoader.Load<ShaderMaterial>("res://Res/Shaders/LightMateral.tres").Duplicate();
					var shader = (ShaderMaterial)tileData.Material;
					shader.SetShaderParameter("LightColor",tile.Value.LightColor);
					shader.SetShaderParameter("LightTexture",tile.Value.LightTexture);
				}

				if (tile.Value.TileMoveType == TileMoveType.Ground)
				{
					for (var id = 1; id < 10; id++)
					{
						source.CreateAlternativeTile(Vector2I.Zero, id);
						tileData = source.GetTileData(Vector2I.Zero, id);
						var newPolygon = new NavigationPolygon();
						newPolygon.SetVertices([
							new Vector2(-16, -16),
							new Vector2(16, -16),
							new Vector2(16, 16),
							new Vector2(-16, 16)
						]);
						newPolygon.AddPolygon([0, 1, 2, 3]);
						tileData.SetNavigationPolygon(id+5, newPolygon);
						tileData.SetCustomData("Rough", tile.Value.Rough);
						tileData.AddCollisionPolygon(layerId);
						tileData.SetCollisionPolygonPoints(layerId, 0,
						[
							new Vector2(-16, -16),
							new Vector2(16, -16),
							new Vector2(16, 16),
							new Vector2(-16, 16)
						]);
						tileData.AddCollisionPolygon(id+6);
						tileData.SetCollisionPolygonPoints(id+6, 0,
						[
							new Vector2(-16, -16),
							new Vector2(16, -16),
							new Vector2(16, 16),
							new Vector2(-16, 16)
						]);
						if (!tile.Value.CanLighted) continue;
						tileData.ZIndex = 5;
						tileData.Material = (Material)ResourceLoader.Load<ShaderMaterial>("res://Res/Shaders/LightMateral.tres").Duplicate();
						var shader = (ShaderMaterial)tileData.Material;
						shader.SetShaderParameter("LightColor",tile.Value.LightColor);
						shader.SetShaderParameter("LightTexture",tile.Value.LightTexture);
					}
				}

				if (tile.Value.TileMoveType != TileMoveType.Ground && tile.Value.TileMoveType != TileMoveType.Water &&
				    tile.Value.TileMoveType != TileMoveType.Bridge)
				{
					tileData.AddCollisionPolygon(16);
					tileData.SetCollisionPolygonPoints(16, 0,
					[
						new Vector2(-16, -16),
						new Vector2(16, -16),
						new Vector2(16, 16),
						new Vector2(-16, 16)
					]);
				}
			}
			index++;
		}
	}
	
	
	private void _setTiles()
	{
		var index = 0;
		foreach (var tile in TileMap.MapTiles)
		{
			var pos = new Vector2I((int)(index%TileMap.MapSize.X), (int)Math.Floor(index/TileMap.MapSize.X));
			_tiles.SetCell(pos, _tileIndex[tile], Vector2I.Zero,
				Instance.Tiles[tile].TileMoveType == TileMoveType.Ground? TileMap.MapHeight[index] : 0);
			index++;
		}
	}
}