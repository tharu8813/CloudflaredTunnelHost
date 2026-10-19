namespace CloudflaredTunnelHost.Core.Models;

public static class TunnelStateMachine
{
    public static bool CanTransition(TunnelState from, TunnelState to) => from == to || to == TunnelState.Stopping
        || (to == TunnelState.Error && from != TunnelState.Idle)
        || (from, to) switch
        {
            (TunnelState.Idle or TunnelState.Error, TunnelState.Validating) => true,
            (TunnelState.Validating or TunnelState.Reconnecting, TunnelState.Starting) => true,
            (TunnelState.Starting, TunnelState.Connecting) => true,
            (TunnelState.Connecting or TunnelState.Reconnecting, TunnelState.Connected) => true,
            (TunnelState.Connecting or TunnelState.Connected, TunnelState.Reconnecting) => true,
            (TunnelState.Stopping or TunnelState.Error, TunnelState.Idle) => true,
            _ => false
        };
}
