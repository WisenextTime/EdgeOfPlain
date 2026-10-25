using System;

namespace EdgeOfPlain.Core.Framework.Mods;
public class ContentResolutionException(string message) : InvalidOperationException(message);
