using System;

namespace EdgeOfPlain.Core.Exceptions;

public class MapException(string message) : Exception(message);