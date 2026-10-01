using Godot;

namespace Exchanger.Core.Session;

internal sealed class GodotMonotonicClock : IMonotonicClock
{
    public ulong NowMilliseconds => Time.GetTicksMsec();
}
