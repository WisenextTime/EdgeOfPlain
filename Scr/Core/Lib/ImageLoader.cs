using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;
using static EdgeOfPlain.Scr.Core.Global.Global;

namespace EdgeOfPlain.Scr.Core.Lib;

public static class ImageLoader
{
	public static Texture2D LoadImage(string imageName,string imageType="",string imageId="",string imageStruct = "", string resPath="res://Res/")
	{
		try
		{
			return imageName switch
			{
				IndexImage.Missing => ResourceLoader.Load<Texture2D>("res://Res/Textures/Missing.png"),
				IndexImage.None => new PlaceholderTexture2D(),
				IndexImage.Default => ResourceLoader.Load<Texture2D>(imageId == "" ? 
					"res://Res/Textures/Missing.png" : 
					ResourceLoader.Exists($"{imageId}{imageStruct}.png") ?
					"res://Res/Textures/Missing.png" : 
					$"{resPath}Textures/{imageType}/{imageId}{imageStruct}.png"),
				IndexImage.Blank => ResourceLoader.Load<Texture2D>("res://Res/Textures/Blank.png"),
				_ => ResourceLoader.Load<Texture2D>(imageName)
			};
		}
		catch (Exception e)
		{
			Console.WriteLine(e);
			throw;
		}
	}
	
	public static SpriteFrames LoadAnimatedImage(string imageName,string imageType="",string imageId="",string imageStruct = "", string resPath="res://Res/")
	{
		try
		{
			return imageName switch
			{
				IndexImage.Missing => ResourceLoader.Load<SpriteFrames>("res://Res/Textures/Missing.tres"),
				IndexImage.Default => ResourceLoader.Load<SpriteFrames>(imageId == ""
					?
					"res://Res/Textures/Missing.tres"
					: ResourceLoader.Exists($"{imageId}{imageStruct}.tres")
						? "res://Res/Textures/Missing.tres"
						:
						$"{resPath}Textures/{imageType}/{imageId}{imageStruct}.tres"),
				IndexImage.Blank => ResourceLoader.Load<SpriteFrames>("res://Res/Textures/Blank.tres"),
				_ => ResourceLoader.Load<SpriteFrames>(imageName)
			};
		}
		catch (Exception e)
		{
			Console.WriteLine(e);
			throw;
		}
	}

	public static Color GetColor(Texture2D image)
	{
		var texture = image.GetImage().Data;
		var colors = (Array<int>)texture["data"];
		List<Vector3> packedColor =[];
		for (var i = 0; i <colors.Count/3; i++)
		{
			packedColor.Add(new Vector3(colors[3*i], colors[3*i + 1], colors[3*i + 2]));
		}
		System.Collections.Generic.Dictionary<Vector3, int> colorList = [];
		foreach (var color in packedColor.Select((color,index) => new { color, index }))
		{
			if (colorList.ContainsKey(packedColor[color.index]))
			{
				colorList[color.color]++;
			}
			else
			{
				colorList.Add(color.color, 1);
			}
		}

		var finalColor = colorList.MaxBy(color => color.Value).Key;
		return new Color(finalColor.X/255, finalColor.Y/255, finalColor.Z/255);
	}
}