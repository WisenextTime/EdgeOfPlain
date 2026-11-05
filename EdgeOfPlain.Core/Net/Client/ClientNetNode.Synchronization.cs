using System;
using System.Threading;
using System.Threading.Tasks;
using EdgeOfPlain.Core.Game;
using EdgeOfPlain.Core.Game.Commands;
using EdgeOfPlain.Core.Game.Snapshots;
using EdgeOfPlain.Core.Net.Synchronization;
using FixedMathSharp;
using LiteNetLib.Utils;

namespace EdgeOfPlain.Core.Net.Client;

public sealed partial class ClientNetNode
{
	private GameCore? _boundCore;
	private Fixed64 _fixedStep;
	private long? _synchronizationId;
	private CancellationTokenSource? _snapshotCancellation;
	private Task<SnapshotTransmission>? _capture;
	private Task<GameSnapshot>? _restore;
	private SnapshotTransmission? _outgoingSnapshot;
	private SnapshotReception? _incomingSnapshot;
	private bool _snapshotApplied;
	private long _synchronizationStarted;

	public bool IsSynchronizing => Room.State == BattleRoomState.Synchronizing;
	public event Action? Synchronized;
	public Exception? SynchronizationError { get; private set; }

	public void BindGameCore(GameCore core, Fixed64 fixedStep)
	{
		ThrowIfDisposed();
		ArgumentNullException.ThrowIfNull(core);
		if (IsSynchronizing) throw new InvalidOperationException("Cannot replace the game during synchronization.");
		if (fixedStep <= Fixed64.Zero) throw new ArgumentOutOfRangeException(nameof(fixedStep));
		_boundCore = core;
		_fixedStep = fixedStep;
	}

	private bool ReceiveSynchronization(TurnMessage message, NetDataReader reader)
	{
		if (message is not (TurnMessage.Pause or TurnMessage.RequestSnapshot or TurnMessage.SnapshotBegin
		    or TurnMessage.SnapshotChunk or TurnMessage.Resume)) return false;
		var id = SynchronizationProtocol.ReadId(reader);
		try
		{
			if (message == TurnMessage.Pause)
			{
				TurnProtocol.Require(IsStarted && _synchronizationId is null && id > 0 && _boundCore is not null);
				TurnProtocol.Require(reader.AvailableBytes == sizeof(long) + sizeof(int));
				var tick = reader.GetLong();
				var administrator = reader.GetInt();
				TurnProtocol.Require(administrator == Room.AdministratorId && AppliedTick <= tick);
				Room.State = BattleRoomState.Synchronizing;
				_synchronizationId = id;
				_synchronizationStarted = _options.TimeProvider.GetTimestamp();
				_snapshotCancellation = new CancellationTokenSource();
				SynchronizationError = null;
				while (AppliedTick < tick)
				{
					TurnProtocol.Require(_frames.TryPeek(out var frame) && frame.Tick == AppliedTick + 1);
					NetworkCommands.Enqueue(frame, _boundCore!);
					_boundCore!.Step(_fixedStep);
					_frames.Dequeue();
					AppliedTick++;
				}
				TurnProtocol.Require(_nextFrameTick == tick + 1 && _frames.Count == 0);
				_boundCore!.Pause();
				SendSynchronization(SynchronizationProtocol.Control(TurnMessage.Paused, id));
				return true;
			}
			TurnProtocol.Require(IsSynchronizing && id == _synchronizationId);
			switch (message)
			{
				case TurnMessage.RequestSnapshot:
					TurnProtocol.RequireEnd(reader);
					TurnProtocol.Require(PlayerId == Room.AdministratorId && _capture is null && _outgoingSnapshot is null);
					_capture = CaptureForSendAsync(id, _snapshotCancellation!.Token);
					break;
				case TurnMessage.SnapshotBegin:
					TurnProtocol.Require(_incomingSnapshot is null && _restore is null && !_snapshotApplied);
					_incomingSnapshot = new SnapshotReception(reader);
					break;
				case TurnMessage.SnapshotChunk:
					TurnProtocol.Require(_incomingSnapshot is not null && _restore is null);
					if (_incomingSnapshot!.Append(reader))
						_restore = PrepareSnapshotAsync(_incomingSnapshot, _snapshotCancellation!.Token);
					break;
				case TurnMessage.Resume:
					TurnProtocol.Require(reader.AvailableBytes == sizeof(long) && _snapshotApplied);
					TurnProtocol.Require(reader.GetLong() == AppliedTick + 1);
					CancelSynchronization();
					_boundCore!.Resume();
					Room.State = BattleRoomState.Running;
					_submissionStarted = _options.TimeProvider.GetTimestamp();
					Synchronized?.Invoke();
					break;
			}
			return true;
		}
		catch(Exception exception) when(exception is not OutOfMemoryException)
		{
			SynchronizationError = exception;
			SendSynchronization(SynchronizationProtocol.Control(TurnMessage.SynchronizationFailed, id));
			FailConnection(ConnectionRejection.SynchronizationFailed);
			return true;
		}
	}

	private async Task<SnapshotTransmission> CaptureForSendAsync(long id, CancellationToken cancellationToken)
	{
		var bytes = await GameSnapshot.CaptureAsync(_boundCore!, cancellationToken).ConfigureAwait(false);
		return SnapshotTransmission.Create(id, bytes);
	}

	private static async Task<GameSnapshot> PrepareSnapshotAsync(SnapshotReception incoming, CancellationToken cancellationToken)
	{
		var bytes = await incoming.CompleteAsync(cancellationToken).ConfigureAwait(false);
		return await GameSnapshot.DecodeAsync(bytes, cancellationToken).ConfigureAwait(false);
	}

	private void SendSynchronization(NetDataWriter writer)
		=> _server?.Send(writer, TurnProtocol.Channel, TurnProtocol.Delivery);

	private void PollSynchronization()
	{
		if (_synchronizationId is not { } id) return;
		if (_options.TimeProvider.GetElapsedTime(_synchronizationStarted) >= _options.SynchronizationTimeout)
		{
			SynchronizationError = new TimeoutException("World synchronization timed out.");
			FailConnection(ConnectionRejection.SynchronizationTimeout);
			return;
		}
		try
		{
			if (_capture is { IsCompleted: true } captured)
			{
				_outgoingSnapshot = captured.GetAwaiter().GetResult();
				_capture = null;
				SendSynchronization(_outgoingSnapshot.Begin());
			}
			if (_outgoingSnapshot is { } outgoing && outgoing.SendChunks(SendSynchronization)) _outgoingSnapshot = null;
			if (_restore is { IsCompleted: true } restored)
			{
				restored.GetAwaiter().GetResult().Apply(_boundCore!);
				_restore = null;
				_incomingSnapshot = null;
				_snapshotApplied = true;
				SendSynchronization(SynchronizationProtocol.Control(TurnMessage.SnapshotApplied, id));
			}
		}
		catch(Exception exception) when(exception is not OutOfMemoryException)
		{
			SynchronizationError = exception;
			SendSynchronization(SynchronizationProtocol.Control(TurnMessage.SynchronizationFailed, id));
			FailConnection(ConnectionRejection.SynchronizationFailed);
		}
	}

	private void CancelSynchronization()
	{
		_snapshotCancellation?.Cancel();
		SynchronizationProtocol.Observe(_capture);
		SynchronizationProtocol.Observe(_restore);
		_snapshotCancellation?.Dispose();
		_snapshotCancellation = null;
		_capture = null;
		_restore = null;
		_outgoingSnapshot = null;
		_incomingSnapshot = null;
		_synchronizationId = null;
		_snapshotApplied = false;
	}
}
