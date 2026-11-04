using System;
using LiteNetLib;

namespace EdgeOfPlain.Core.Net.Client;

public class ServerNetNode : INetNode
{
	private readonly EventBasedNetListener _listener;
	private readonly NetManager _manager;
	private bool _isDisposed;

	public ServerNetNode(int port)
	{
		ThrowIfDisposed();
		_listener = new EventBasedNetListener();
		_manager = new NetManager(_listener);
		_manager.Start(port);
	}

	public void Dispose()
	{
		if (_isDisposed) return;
		_isDisposed = true;
		_manager.Stop();
	}

	private void ThrowIfDisposed()
	{
		if (_isDisposed) throw new InvalidOperationException("Server is already disposed.");
	}
}