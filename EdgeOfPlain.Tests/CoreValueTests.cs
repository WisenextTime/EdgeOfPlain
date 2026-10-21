using System;
using System.Drawing;
using System.Linq;
using System.Numerics;
using EdgeOfPlain.Core;
using EdgeOfPlain.Core.Definitions;
using EdgeOfPlain.Core.Framework.Graphics;
using EdgeOfPlain.Core.Framework.Mods;
using EdgeOfPlain.Core.Framework.Time;
using Xunit;

namespace EdgeOfPlain.Tests;

[ContentTypeName("value-probe")]
internal sealed class ValueProbeDef(string id) : ContentDef(id);

public sealed class ColorFTests
{
	[Fact]
	public void FromRgb_ExtractsComponentsAndOpaqueAlpha()
	{
		var color = ColorF.FromRgb(0x123456);

		Assert.Equal((byte)0x12, color.R);
		Assert.Equal((byte)0x34, color.G);
		Assert.Equal((byte)0x56, color.B);
		Assert.Equal((byte)255, color.A);
	}

	[Fact]
	public void FromRgb_IgnoresBitsAboveRgbRange()
	{
		Assert.Equal(new ColorF(0xAB, 0xCD, 0xEF, 255), ColorF.FromRgb(0xFFABCDEF));
		Assert.Equal(new ColorF(0, 0, 0, 255), ColorF.FromRgb(0));
	}

	[Fact]
	public void PresetsAndValueEquality()
	{
		Assert.Equal(new ColorF(255, 255, 255, 255), ColorF.White);
		Assert.Equal(new ColorF(0, 0, 0, 255), ColorF.Black);
		Assert.NotEqual(ColorF.White, ColorF.Black);
		Assert.Equal(new ColorF(1, 2, 3, 4), new ColorF(1, 2, 3, 4));
		Assert.NotEqual(new ColorF(1, 2, 3, 4), new ColorF(1, 2, 3, 5));
		Assert.True(new ColorF(1, 2, 3, 4) == new ColorF(1, 2, 3, 4));
	}
}

public sealed class GlobalsTests
{
	[Fact]
	public void GameVersion_IsOneZeroZero()
	{
		Assert.Equal(new Version(1, 0, 0), Globals.GameVersion);
		Assert.Equal(1, Globals.GameVersion.Major);
		Assert.Equal(0, Globals.GameVersion.Minor);
		Assert.Equal(0, Globals.GameVersion.Build);
	}
}

public sealed class RenderMetadataTests
{
	[Fact]
	public void DefaultsAreNeutral()
	{
		var metadata = new RenderMetadata();

		Assert.Null(metadata.Texture);
		Assert.Null(metadata.Shader);
		Assert.Equal(0u, metadata.Layer);
		Assert.Equal(ColorF.White, metadata.Color);
		Assert.Null(metadata.Source);
		Assert.Null(metadata.Size);
		Assert.Equal(Vector2.One, metadata.Scale);
		Assert.Equal(0f, metadata.Rotation);
		Assert.Equal(Vector2.Zero, metadata.Origin);
	}

	[Fact]
	public void WithExpression_CopiesAndOverridesValues()
	{
		var original = new RenderMetadata { Texture = "a", Layer = 3 };
		var changed = original with { Texture = "b", Rotation = 1.5f, Source = new Rectangle(1, 2, 3, 4) };

		Assert.Equal("a", original.Texture);
		Assert.Equal("b", changed.Texture);
		Assert.Equal(3u, changed.Layer);
		Assert.Equal(1.5f, changed.Rotation);
		Assert.Equal(new Rectangle(1, 2, 3, 4), changed.Source);
	}
}

public sealed class DefinitionDefaultTests
{
	[Fact]
	public void TilesetDef_HasExpectedDefaults()
	{
		var tileset = new TilesetDef("tiles");

		Assert.Equal(0, tileset.Width);
		Assert.Equal(0, tileset.Height);
		Assert.Equal(32, tileset.TileWidth);
		Assert.Equal(32, tileset.TileHeight);
		Assert.Equal(0, tileset.LeftMargin);
		Assert.Equal(0, tileset.TopMargin);
		Assert.Equal(0, tileset.RightMargin);
		Assert.Equal(0, tileset.BottomMargin);
		Assert.Equal(0, tileset.HorizontalSpacing);
		Assert.Equal(0, tileset.VerticalSpacing);
		Assert.Empty(tileset.TileData);
	}

	[Fact]
	public void TileData_FallsBackWhenUnset()
	{
		var unset = default(TileData);
		var blank = new TileData();

		Assert.Equal("ground", unset.Terrain);
		Assert.Equal("default", unset.Renderer);
		Assert.Equal("ground", blank.Terrain);
		Assert.Equal("default", blank.Renderer);
		Assert.Null(unset.Animation);
		Assert.Null(unset.Random);

		var configured = new TileData
		{
			Terrain = "water",
			Renderer = "animated",
			Animation = [1, 2],
			Random = [3],
		};

		Assert.Equal("water", configured.Terrain);
		Assert.Equal("animated", configured.Renderer);
		Assert.Equal(new[] { 1, 2 }, configured.Animation);
		Assert.Equal(new[] { 3 }, configured.Random);
	}

	[Fact]
	public void TileRendererDef_HasNoShaderAndNoUniformsByDefault()
	{
		var renderer = new TileRendererDef("renderer");

		Assert.Equal(string.Empty, renderer.ShaderPath);
		Assert.Empty(renderer.Uniforms);

		renderer.Uniforms["tint"] = new ShaderUniform
		{
			Type = ShaderUniform.UniformType.Vec4,
			Value = [1f, 0f, 0f, 1f],
			Texture = "tex",
		};

		Assert.Equal(ShaderUniform.UniformType.Vec4, renderer.Uniforms["tint"].Type);
		Assert.Equal(new[] { 1f, 0f, 0f, 1f }, renderer.Uniforms["tint"].Value);
		Assert.Equal("tex", renderer.Uniforms["tint"].Texture);
	}

	[Fact]
	public void ShaderUniform_DefaultsToFloatWithoutValue()
	{
		var uniform = default(ShaderUniform);

		Assert.Equal(ShaderUniform.UniformType.Float, uniform.Type);
		Assert.Null(uniform.Value);
		Assert.Null(uniform.Texture);
	}
}

public sealed class ContentDefIdentityTests
{
	private static readonly ModInfo _alpha = new("alpha", new Version(1, 0, 0));

	[Fact]
	public void FullId_CombinesModDiscriminatorAndId()
	{
		var def = new ValueProbeDef("thing") { SourceMod = _alpha };

		Assert.Equal("alpha.value-probe(thing)", def.FullId);
		Assert.Equal(def.FullId, def.ToString());
	}

	[Fact]
	public void FullId_ThrowsWhenSourceModWasNeverAssigned()
	{
		var def = new ValueProbeDef("thing");

		Assert.Throws<InvalidOperationException>(() => def.FullId);
		Assert.Throws<InvalidOperationException>(() => def.ToString());
		var exception = Assert.Throws<InvalidOperationException>(() => def.SourceMod);
		Assert.Contains("source mod", exception.Message, StringComparison.OrdinalIgnoreCase);
	}

	[Fact]
	public void CompareTo_OrdersByFullId()
	{
		var first = new ValueProbeDef("a") { SourceMod = _alpha };
		var second = new ValueProbeDef("b") { SourceMod = _alpha };
		var otherMod = new ValueProbeDef("a") { SourceMod = new ModInfo("beta", new Version(1, 0, 0)) };

		Assert.Equal(0, first.CompareTo(first));
		Assert.Equal(0, first.CompareTo(new ValueProbeDef("a") { SourceMod = _alpha }));
		Assert.True(first.CompareTo(second) < 0);
		Assert.True(second.CompareTo(first) > 0);
		Assert.True(first.CompareTo(otherMod) < 0);
	}

	[Fact]
	public void CompareTo_TreatsNullAsOrderingFirst()
	{
		var def = new ValueProbeDef("a") { SourceMod = _alpha };

		Assert.True(def.CompareTo(null) > 0);
		Assert.Equal(new[] { "alpha.value-probe(a)", "alpha.value-probe(b)" },
			new[] { new ValueProbeDef("b") { SourceMod = _alpha }, new ValueProbeDef("a") { SourceMod = _alpha } }
				.OrderBy(item => item)
				.Select(item => item.FullId)
				.ToArray());
	}
}

public sealed class SmallLoopAndCameraTests
{
	[Fact]
	public void GameLoop_DoesNotStepBeforeBeginFrame()
	{
		var loop = new GameLoop(new FakeTimeSource());

		Assert.False(loop.TryStep(out var time));
		Assert.Equal(default(LogicTime), time);
		Assert.Equal(0, loop.Tick);
		Assert.Equal(0, loop.PendingSteps);
		Assert.Equal(0f, loop.Alpha);
		Assert.Equal(GameLoop.DefaultFixedStep, loop.FixedStep);
		Assert.Equal(0d, loop.TotalSeconds);
	}

	[Fact]
	public void CameraWorldBounds_AreCenteredOnPosition()
	{
		var camera = new Camera2D(new Vector2(100, 50), 2f);

		var bounds = camera.WorldBounds(new Vector2(1280, 720));

		Assert.Equal(-220f, bounds.Left, 3);
		Assert.Equal(-130f, bounds.Top, 3);
		Assert.Equal(640f, bounds.Width, 3);
		Assert.Equal(360f, bounds.Height, 3);
	}
}
