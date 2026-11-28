using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EdgeOfPlain.Core.Net.Synchronization;
using LiteNetLib;
using LiteNetLib.Utils;

namespace EdgeOfPlain.Core.Net.Server
{
	public sealed partial class ServerNetNode
	{
		private long _synchronizationSequence;
		private SynchronizationSession? _synchronization;

		private int SynchronizingPlayerCount
			=> _slots.Count(slot => slot is { Active: true, IsHuman: true, Peer: not null });

		public Task SynchronizeAsync(CancellationToken cancellationToken = default)
		{
			ThrowIfDisposed();
			cancellationToken.ThrowIfCancellationRequested();
			if (!IsStarted || _synchronization is not null || Room.AdministratorId is not { } administrator)
				throw new InvalidOperationException("A running room with an administrator is required.");
			var slot = _slots.Find(candidate => candidate.Active && candidate.Id == administrator && candidate.Peer is not null)
				?? throw new InvalidOperationException("The administrator is not connected.");
			var session = new SynchronizationSession(++_synchronizationSequence, NextTick - 1,
				slot.Peer!, _options.TimeProvider.GetTimestamp(), cancellationToken);
			_synchronization = session;
			Room.State = BattleRoomState.Synchronizing;
			var writer = SynchronizationProtocol.Control(TurnMessage.Pause, session.Id);
			writer.Put(session.Tick);
			writer.Put(administrator);
			Broadcast(writer);
			return session.Completion.Task;
		}

		private bool CanReceivePausedSubmission(NetPeer peer)
			=> _synchronization is { } session && !session.Paused.Contains(peer);

		private bool ReceiveSynchronization(NetPeer peer, TurnMessage message, NetDataReader reader)
		{
			if (message is not (TurnMessage.Paused or TurnMessage.SnapshotBegin or TurnMessage.SnapshotChunk
			    or TurnMessage.SnapshotApplied or TurnMessage.SynchronizationFailed)) return false;
			var id = SynchronizationProtocol.ReadId(reader);
			TurnProtocol.Require(_synchronization is not null && _byPeer.ContainsKey(peer) && id == _synchronization.Id);
			var session = _synchronization!;
			switch (message)
			{
				case TurnMessage.Paused:
					TurnProtocol.RequireEnd(reader);
					TurnProtocol.Require(session.Phase == SynchronizationPhase.Pausing && session.Paused.Add(peer));
					if (session.Paused.Count == SynchronizingPlayerCount)
					{
						session.Phase = SynchronizationPhase.Receiving;
						session.Administrator.Send(SynchronizationProtocol.Control(TurnMessage.RequestSnapshot, id),
							TurnProtocol.Channel, TurnProtocol.Delivery);
					}
					break;
				case TurnMessage.SnapshotBegin:
					TurnProtocol.Require(Equals(peer, session.Administrator) && session is
						{ Phase: SynchronizationPhase.Receiving, Incoming: null, Validation: null });
					session.Incoming = new SnapshotReception(reader);
					break;
				case TurnMessage.SnapshotChunk:
					TurnProtocol.Require(Equals(peer, session.Administrator) && session is
						{ Phase: SynchronizationPhase.Receiving, Incoming: not null, Validation: null });
					if (session.Incoming!.Append(reader))
						session.Validation = session.Incoming.CompleteAsync(session.Cancellation.Token);
					break;
				case TurnMessage.SnapshotApplied:
					TurnProtocol.RequireEnd(reader);
					TurnProtocol.Require(session is { Phase: SynchronizationPhase.Applying, Outgoing: null } &&
					                     session.Applied.Add(peer));
					if (session.Applied.Count == SynchronizingPlayerCount) ResumeSynchronizedRoom(session);
					break;
				case TurnMessage.SynchronizationFailed:
					TurnProtocol.RequireEnd(reader);
					CloseRoom(ConnectionRejection.SynchronizationFailed, new InvalidOperationException("A client could not synchronize."));
					break;
			}
			return true;
		}

		private void PollSynchronization()
		{
			if (_synchronization is not { } session) return;
			if (session.Cancellation.IsCancellationRequested)
			{
				CloseRoom(ConnectionRejection.SynchronizationFailed, new OperationCanceledException(session.Cancellation.Token));
				return;
			}
			if (_options.TimeProvider.GetElapsedTime(session.Started) >= _options.SynchronizationTimeout)
			{
				CloseRoom(ConnectionRejection.SynchronizationTimeout, new TimeoutException("World synchronization timed out."));
				return;
			}
			try
			{
				if (session.Validation is { IsCompleted: true } task)
				{
					var bytes = task.GetAwaiter().GetResult();
					session.Validation = null;
					session.Incoming = null;
					session.Outgoing = SnapshotTransmission.Create(session.Id, bytes);
					session.Phase = SynchronizationPhase.Applying;
					Broadcast(session.Outgoing.Begin());
				}
				if (session.Outgoing is { } outgoing && outgoing.SendChunks(Broadcast)) session.Outgoing = null;
			}
			catch(Exception exception) when(exception is not OutOfMemoryException)
			{
				CloseRoom(ConnectionRejection.SynchronizationFailed, exception);
			}
		}

		private void ResumeSynchronizedRoom(SynchronizationSession session)
		{
			var writer = SynchronizationProtocol.Control(TurnMessage.Resume, session.Id);
			writer.Put(NextTick);
			Broadcast(writer);
			Room.State = BattleRoomState.Running;
			_phaseStarted = _options.TimeProvider.GetTimestamp();
			_nextTurnAt = _phaseStarted;
			_synchronization = null;
			session.Completion.TrySetResult();
			session.Cancellation.Dispose();
		}

		private void EndSynchronization(Exception failure)
		{
			if (_synchronization is not { } session) return;
			_synchronization = null;
			if (session.Cancellation.IsCancellationRequested)
				session.Completion.TrySetCanceled(session.Cancellation.Token);
			else session.Completion.TrySetException(failure);
			session.Cancellation.Cancel();
			SynchronizationProtocol.Observe(session.Validation);
			session.Cancellation.Dispose();
		}

		private enum SynchronizationPhase { Pausing, Receiving, Applying }

		private sealed class SynchronizationSession(long id, long tick, NetPeer administrator, long started, CancellationToken token)
		{
			public long Id { get; } = id;
			public long Tick { get; } = tick;
			public long Started { get; } = started;
			public NetPeer Administrator { get; } = administrator;
			public CancellationTokenSource Cancellation { get; } = CancellationTokenSource.CreateLinkedTokenSource(token);
			public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
			public HashSet<NetPeer> Paused { get; } = [];
			public HashSet<NetPeer> Applied { get; } = [];
			public SynchronizationPhase Phase { get; set; }
			public SnapshotReception? Incoming { get; set; }
			public Task<byte[]>? Validation { get; set; }
			public SnapshotTransmission? Outgoing { get; set; }
		}
	}
}
