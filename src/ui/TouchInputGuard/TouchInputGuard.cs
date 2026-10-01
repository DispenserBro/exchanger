using Godot;

namespace Exchanger.UI.TouchInputGuard;

/// <summary>
/// Централизованно отбрасывает все касания кроме первого, чтобы случайный
/// мультитач не запускал второе действие вендинговой сессии.
/// </summary>
public partial class TouchInputGuard : Node
{
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Input(InputEvent inputEvent)
    {
        bool isSecondaryTouch = inputEvent switch
        {
            InputEventScreenTouch touch => touch.Index > 0,
            InputEventScreenDrag drag => drag.Index > 0,
            _ => false,
        };

        if (isSecondaryTouch)
        {
            GetViewport().SetInputAsHandled();
        }
    }
}
