using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using static EdgeOfPlain.Scr.Core.Global.Global;

namespace EdgeOfPlain.Scr.Core.Resources;

public class GameTileMap
{
	public string MapName { get; set; }
	public string MapAuthor { get; set; }
	public Vector2 MapSize { get; set; }
	public List<string> MapTiles { get; set; } = [];
	public List<int> MapHeight { get; set; } = [];
	public List<Dictionary<string,object>> MapObjects { get; set; } = [];

	public Dictionary<string, object> ToSerializableDictionary()
	{
		var dictionary = new Dictionary<string, object>()
		{
			["MapName"] = MapName,
			["MapAuthor"] = MapAuthor,
			["MapSize"] = new[] { (int)MapSize.X, (int)MapSize.Y },
			["MapTiles"] = MapTiles,
			["MapHeight"] = MapHeight,
			["MapObjects"] = MapObjects
		};
		return dictionary;
	}

	public void ParseFromDictionary(Dictionary<string, object> dictionary)
	{
		MapName = dictionary["MapName"] as string;
		MapAuthor = dictionary["MapAuthor"] as string;
		if (dictionary["MapSize"] is int[] mapSizeArray) MapSize = new Vector2(mapSizeArray[0], mapSizeArray[1]);
		MapTiles = dictionary["MapTiles"] as List<string>;
		MapHeight = dictionary["MapHeight"] as List<int>;
		MapObjects = dictionary["MapObjects"] as List<Dictionary<string, object>>;
	}
	
}

public static class MapParser
{
	public static GameTileMap NewTileMap(string name,string author,Vector2 size)
	{
		var map = new GameTileMap()
		{
			MapName = name,
			MapAuthor = author,
			MapSize = size,
		};
		for (var i = 0; i < map.MapSize.X * map.MapSize.Y; i++)
		{
			map.MapTiles.Add("Grass");
			map.MapHeight.Add(0);
		}
		return map;
	}

	public static void Save(GameTileMap map, string filePath)
	{
		var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
#pragma warning disable SYSLIB0011
		BinaryFormatter formatter = new();
#pragma warning restore SYSLIB0011
#pragma warning disable SYSLIB0011
		formatter.Serialize(fileStream, map.ToSerializableDictionary());
#pragma warning restore SYSLIB0011
		fileStream.Close();
	}

	public static GameTileMap Load(string filePath)
	{
#pragma warning disable SYSLIB0011
		BinaryFormatter formatter = new();
#pragma warning restore SYSLIB0011
		var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
#pragma warning disable SYSLIB0011
		var mapData = formatter.Deserialize(fileStream) as Dictionary<string,object>;
#pragma warning restore SYSLIB0011
		GameTileMap map = new();
		map.ParseFromDictionary(mapData);
		fileStream.Close();
		return map;
	}
}