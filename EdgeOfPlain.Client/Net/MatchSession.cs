using System;
using System.Collections.Generic;
using System.Net;
using EdgeOfPlain.Core.Definitions.Worlds;
using EdgeOfPlain.Core.Framework.Time;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Net;
using EdgeOfPlain.Core.Net.Ai;
using EdgeOfPlain.Core.Net.Client;
using EdgeOfPlain.Core.Net.Server;
using FixedMathSharp;

namespace EdgeOfPlain.Client.Net;

public sealed class MatchSession : IDisposable
{
	private const int DefaultPort = 27015;

	private readonly Dictionary<int, IAiClient> _aiClients = [];
	private readonly MemoryMapCache _mapCache = new();
	private bool _disposed;

	private MatchSession(ServerNetNode? server, ClientNetNode client)
	{
		Server = server;
		Client = client;
		client.MapCache = _mapCache;
	}

	public static MatchSession Host(string playerName, int port = DefaultPort, int maxPlayers = 8)
	{
		var server = new ServerNetNode(port, maxPlayers);
		try
		{
			var client = new ClientNetNode(IPAddress.Loopback.ToString(), server.LocalPort, playerName: playerName);
			return new MatchSession(server, client);
		}
		catch
		{
			server.Dispose();
			throw;
		}
	}

	public static MatchSession Join(string playerName, string host, int port = DefaultPort)
		=> new(null, new ClientNetNode(host, port, playerName: playerName));

	public ClientNetNode Client { get; }

	public ServerNetNode? Server { get; }

	public GameCore? Core { get; private set; }

	public WorldKey? MapKey { get; private set; }

	public GameMap? GameMap { get; private set; }

	public bool IsHost => Server is not null;

	public bool IsAdministrator => Client.PlayerId is { } id && Client.Room.AdministratorId == id;

	public bool IsStarted => Client.IsStarted;

	public void StartMatch(Map map)
	{
		if (Server is null) throw new InvalidOperationException("Only the room creator can start the match.");
		Server.StartMatch(map);
	}

	public void Poll()
	{
		ThrowIfDisposed();
		Server?.PollEvents();
		Client.PollEvents();

		if (Core is null && Client.PendingMap is { } map)
		{
			GameMap ??= GameMap.Build(map);
			Core = new GameCore();
			MapKey = Client.PendingMapKey ?? WorldKey.Primary;
			var world = Core.CreateWorld(MapKey.Value, GameMap);
			Client.BindGameCore(Core, GameLoop.DefaultFixedStep);
			Client.MarkWorldReady(world);
		}

		if (!IsAdministrator) return;
		foreach (var player in Client.Room.Players)
			if (player.Kind == RoomPlayerKind.Ai)
				_aiClients.TryAdd(player.Id, EmptyAiClient.Instance);
	}

	public void AssignTeam(int playerId, int team) => Client.RequestAssignTeam(playerId, team);

	public void AddAi(string name) => Client.RequestAddAi(name);

	public void SubmitAi(long tick)
	{
		if (!IsAdministrator) return;
		foreach (var player in Client.Room.Players)
		{
			if (player.Kind != RoomPlayerKind.Ai) continue;
			var ai = _aiClients.TryGetValue(player.Id, out var client) ? client : EmptyAiClient.Instance;
			Client.SubmitAi(player.Id, tick, ai.BuildCommands(tick));
		}
	}

	public bool TryStep(long tick, Fixed64 delta)
	{
		ThrowIfDisposed();
		return Core is not null && Client.TryStepWorld(tick, Core, delta);
	}

	public void CacheMap(WorldKey key, in Map map)
	{
		if (Client.PendingMapInfo is { } info) _mapCache.Set(key, info.MapId, info.Checksum, map);
	}

	public void Dispose()
	{
		if (_disposed) return;
		_disposed = true;
		Client.Dispose();
		Server?.Dispose();
		MapKey = null;
		GameMap = null;
		Core?.Dispose();
		Core = null;
	}

	private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

	private sealed class MemoryMapCache : IMapCache
	{
		private readonly Dictionary<WorldKey, (string MapId, string Checksum, Map Map)> _entries = [];

		public bool TryGet(WorldKey key, string mapId, string checksum, out Map map)
		{
			if (_entries.TryGetValue(key, out var entry) &&
			    string.Equals(entry.MapId, mapId, StringComparison.Ordinal) &&
			    string.Equals(entry.Checksum, checksum, StringComparison.Ordinal))
			{
				map = entry.Map;
				return true;
			}

			map = default;
			return false;
		}

		public void Set(WorldKey key, string mapId, string checksum, in Map map)
			=> _entries[key] = (mapId, checksum, map);
	}
}
