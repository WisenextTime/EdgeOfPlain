using System;
using Godot;
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
}