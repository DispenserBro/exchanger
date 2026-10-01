using Exchanger.Hardware.Abstractions;
using Godot;

namespace Exchanger.UI.ConnectionStatusIndicator;

/// <summary>
/// Ненавязчивый сервисный индикатор. В штатном production-состоянии скрыт,
/// но сообщает безопасный статус при авторизации, обрыве или ошибке связи.
/// </summary>
public partial class ConnectionStatusIndicator : MarginContainer
{
    private static readonly Color ReadyColor = Color.FromHtml("77D18B");
    private static readonly Color WaitingColor = Color.FromHtml("F4C95D");
    private static readonly Color ErrorColor = Color.FromHtml("FF6B6B");

    private Label _label = null!;
    private MachineConnectionState _state = MachineConnectionState.Disconnected;
    private bool _showReadyState;

    public override void _Ready()
    {
        _label = GetNode<Label>("Panel/Margin/Label");
        ApplyState();
    }

    public void Configure(bool showReadyState)
    {
        _showReadyState = showReadyState;
        if (IsNodeReady())
        {
            ApplyState();
        }
    }

    public void SetState(MachineConnectionState state)
    {
        _state = state;
        if (IsNodeReady())
        {
            ApplyState();
        }
    }

    private void ApplyState()
    {
        Visible = _state != MachineConnectionState.Ready || _showReadyState;
        (_label.Text, Color color) = _state switch
        {
            MachineConnectionState.Authenticating => ("СЕРВИС: АВТОРИЗАЦИЯ КОНТРОЛЛЕРА…", WaitingColor),
            MachineConnectionState.Ready => ("СЕРВИС: КОНТРОЛЛЕР ПОДКЛЮЧЁН", ReadyColor),
            MachineConnectionState.Faulted => ("СЕРВИС: КОНТРОЛЛЕР НЕДОСТУПЕН", ErrorColor),
            _ => ("СЕРВИС: НЕТ СВЯЗИ С КОНТРОЛЛЕРОМ", ErrorColor),
        };
        _label.AddThemeColorOverride("font_color", color);
    }
}
