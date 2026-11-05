using System;
using LiteNetLib;

namespace EdgeOfPlain.Core.Net;

public sealed record NetworkOptions
{
	public static NetworkOptions Default { get; } = new();

	public int? DisconnectTimeoutMs { get; init; }
	public int? ConnectRetryDelayMs { get; init; }
	public int? MaxConnectAttempts { get; init; }
	public int? PingIntervalMs { get; init; }
	public int? MaxPendingConnections { get; init; }
	public TimeSpan JoinTimeout { get; init; } = TimeSpan.FromSeconds(10);
	public TimeSpan LoadingTimeout { get; init; } = TimeSpan.FromSeconds(60);
	public TimeSpan TurnTimeout { get; init; } = TimeSpan.FromSeconds(15);
	public TimeSpan SynchronizationTimeout { get; init; } = TimeSpan.FromSeconds(60);
	public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

	internal NetManager CreateManager(EventBasedNetListener listener)
	{
		ValidatePositive(DisconnectTimeoutMs, nameof(DisconnectTimeoutMs));
		ValidatePositive(ConnectRetryDelayMs, nameof(ConnectRetryDelayMs));
		ValidatePositive(MaxConnectAttempts, nameof(MaxConnectAttempts));
		ValidatePositive(PingIntervalMs, nameof(PingIntervalMs));
		ValidatePositive(MaxPendingConnections, nameof(MaxPendingConnections));
		ArgumentNullException.ThrowIfNull(TimeProvider);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(JoinTimeout, TimeSpan.Zero);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(LoadingTimeout, TimeSpan.Zero);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(TurnTimeout, TimeSpan.Zero);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(SynchronizationTimeout, TimeSpan.Zero);

		var manager = new NetManager(listener);
		if (DisconnectTimeoutMs is { } timeout) manager.DisconnectTimeout = timeout;
		if (ConnectRetryDelayMs is { } retryDelay) manager.ReconnectDelay = retryDelay;
		if (MaxConnectAttempts is { } attempts) manager.MaxConnectAttempts = attempts;
		if (PingIntervalMs is { } pingInterval) manager.PingInterval = pingInterval;
		return manager;
	}

	private static void ValidatePositive(int? value, string name)
	{
		if (value is <= 0) throw new ArgumentOutOfRangeException(name, "Must be positive when specified.");
	}
}
