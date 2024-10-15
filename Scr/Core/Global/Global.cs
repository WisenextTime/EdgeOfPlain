using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Runtime.InteropServices;
using EdgeOfPlain.Scr.Core.Resources;
using Godot;

namespace EdgeOfPlain.Scr.Core.Global;

[GlobalClass]
public partial class Global : Node
{
    public Dictionary<string, Tile> Tiles = new();
    public Dictionary<string, Unit> Units = new();
    public static Global Instance { get; private set; }

    public int DeviceType;

    public string GameMapPath;


    public enum TileMoveType
    {
        None, Ground, Water, Bridge, Air, Void
    }
    [Flags]
    public enum UnitMoveType
    {
        None = 0,
        Ground = 1 << 0,
        Water = 1 << 1,
        Air = 1 << 2,
        Hover = 1 << 3,
        Any = 1 << 4
    }

    public static class IndexImage
    {
        public const string Default = "IndexImage.Default";
        public const string None = "IndexImage.None";
        public const string Missing = "IndexImage.Missing";
        public const string Blank = "IndexImage.Blank";
    }
    public enum ImageType
    {
        Default,
        ImageSheet
    }
    public override void _Ready()
    {
        Instance = this;
        DeviceType = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? 0 :
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? 1 : 2;
    }
#if DEBUG
    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey eventKey) return;
        if (eventKey.Keycode == Key.Escape)
        {
            GetTree().Quit();
        }
    }
#endif
}