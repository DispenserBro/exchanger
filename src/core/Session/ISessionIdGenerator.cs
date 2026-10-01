using System;

namespace Exchanger.Core.Session;

public interface ISessionIdGenerator
{
    string CreateId();
}

internal sealed class GuidSessionIdGenerator : ISessionIdGenerator
{
    public string CreateId() => Guid.NewGuid().ToString("N");
}
