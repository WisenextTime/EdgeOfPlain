using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using EdgeOfPlain.Core.Game.Commands;
using FixedMathSharp;


namespace EdgeOfPlain.Core.Game
{
	public sealed class GameCore : IDisposable, ICommandSink
	{
		private readonly Dictionary<WorldKey, GameWorld> _byKey = [];
		private readonly List<GameWorld> _worlds = [];

		public GameCore()
		{
			Dispatcher = new CoreCommandDispatcher(Commands);
		}

		public int Count => _worlds.Count;
		public bool IsPaused { get; private set; }
		public CommandBus Commands { get; } = new();
		public CoreCommandDispatcher Dispatcher { get; }

		public void Pause()
		{
			IsPaused = true;
			foreach (var world in _worlds) world.IsPaused = true;
		}

		public void Resume()
		{
			foreach (var world in _worlds) world.IsPaused = false;
			IsPaused = false;
		}

		public IReadOnlyList<GameWorld> Worlds => _worlds;

		public GameWorld CreateWorld(WorldKey key, GameMap map)
		{
			if (!key.IsValid) throw new ArgumentOutOfRangeException(nameof(key), key, "World key must be positive.");
			if (_byKey.ContainsKey(key)) throw new InvalidOperationException($"World '{key}' already exists.");

			var world = new GameWorld(map, key) { IsPaused = IsPaused };
			_byKey.Add(key, world);
			_worlds.Add(world);
			return world;
		}

		public bool TryGetWorld(WorldKey key, [NotNullWhen(true)]out GameWorld? world) => _byKey.TryGetValue(key, out world);

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
			if (command is IWorldScopedCommand scoped)
			{
				GetWorld(RequireWorldKey(scoped, command)).Commands.Enqueue(in command);
				return;
			}

			Commands.Enqueue(in command);
		}

		internal void ValidateDestination(ICommand command)
		{
			if (command is IWorldScopedCommand scoped) GetWorld(RequireWorldKey(scoped, command));
		}

		public int Step(Fixed64 delta)
		{
			if (IsPaused) return 0;

			var applied = Dispatcher.ApplyAll(this);
			foreach (var world in _worlds) applied += world.Step(delta);
			return applied;
		}

		public int Step() => Step(Fixed64.Zero);

		internal void ReplaceWorlds(IReadOnlyList<GameWorld> states)
		{
			var replacement = new List<GameWorld>(states.Count);
			var retained = new HashSet<WorldKey>();
			foreach (var state in states)
			{
				retained.Add(state.Key);
				if (_byKey.TryGetValue(state.Key, out var existing))
				{
					existing.Adopt(state);
					replacement.Add(existing);
				}
				else replacement.Add(state);
			}
			foreach (var world in _worlds)
				if (!retained.Contains(world.Key)) world.Dispose();
			_worlds.Clear();
			_byKey.Clear();
			foreach (var world in replacement)
			{
				world.IsPaused = IsPaused;
				_worlds.Add(world);
				_byKey.Add(world.Key, world);
			}
		}

		public void Dispose()
		{
			foreach (var world in _worlds) world.Dispose();
			_worlds.Clear();
			_byKey.Clear();
			Commands.Clear();
		}

		private static WorldKey RequireWorldKey(IWorldScopedCommand scoped, ICommand command)
		{
			if (scoped.WorldKey.IsValid) return scoped.WorldKey;

			throw new InvalidOperationException(
				$"Command '{command.GetType().FullName}' is world-scoped but has no valid world key.");
		}
	}
}
