using System;
using System.Collections.Generic;
using EdgeOfPlain.Core.Game.Commands;
using FixedMathSharp;


namespace EdgeOfPlain.Core.Game;

public sealed class GameCore : IDisposable, ICommandSink
{
	private readonly Dictionary<WorldKey, GameWorld> _byKey = [];
	private readonly List<GameWorld> _worlds = [];
	private WorldKey _primaryKey = WorldKey.Invalid;

	public int Count => _worlds.Count;

	public IReadOnlyList<GameWorld> Worlds => _worlds;

	public WorldKey PrimaryKey => _primaryKey;

	public GameWorld? PrimaryWorld => _byKey.GetValueOrDefault(_primaryKey);

	public GameWorld CreateWorld(WorldKey key, GameMap map)
	{
		if (!key.IsValid) throw new ArgumentOutOfRangeException(nameof(key), key, "World key must be positive.");
		if (_byKey.ContainsKey(key)) throw new InvalidOperationException($"World '{key}' already exists.");

		var world = new GameWorld(map, key);
		_byKey.Add(key, world);
		_worlds.Add(world);
		if (!_primaryKey.IsValid) _primaryKey = key;
		return world;
	}

	public bool TryGetWorld(WorldKey key, out GameWorld? world) => _byKey.TryGetValue(key, out world);

	public GameWorld GetWorld(WorldKey key)
	{
		if (_byKey.TryGetValue(key, out var world)) return world;

		throw new InvalidOperationException(
			$"World '{key}' is not part of this game core. Known worlds: {string.Join(", ", _byKey.Keys)}.");
	}

	public WorldKey KeyOf(GameWorld world)
	{
		ArgumentNullException.ThrowIfNull(world);
		return world.Key;
	}

	public void Enqueue<TCommand>(in TCommand command) where TCommand : struct, ICommand
	{
		if (command is IWorldScopedCommand { WorldKey.IsValid: true } scoped)
			GetWorld(scoped.WorldKey).Commands.Enqueue(in command);
		else ResolvePrimary().Commands.Enqueue(in command);
	}

	public int Step(Fixed64 delta)
	{
		var applied = 0;
		foreach (var world in _worlds) applied += world.Step(delta);
		return applied;
	}

	public int Step() => Step(Fixed64.Zero);

	public void Dispose()
	{
		foreach (var world in _worlds) world.Dispose();
		_worlds.Clear();
		_byKey.Clear();
		_primaryKey = WorldKey.Invalid;
	}

	private GameWorld ResolvePrimary() => PrimaryWorld
	                                      ?? throw new InvalidOperationException(
		                                      "Game core has no world; call CreateWorld first.");
}