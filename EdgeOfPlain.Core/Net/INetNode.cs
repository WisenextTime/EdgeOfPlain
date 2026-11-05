using System;

namespace EdgeOfPlain.Core.Net;

public interface INetNode : IDisposable
{
	void PollEvents();
}
