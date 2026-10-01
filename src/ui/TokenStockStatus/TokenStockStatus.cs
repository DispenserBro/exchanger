using Godot;

namespace Exchanger.UI.TokenStockStatus;

public partial class TokenStockStatus : PanelContainer
{
    private Label _stateLabel = null!;
    private Label _approximateCountLabel = null!;
    private ProgressBar _meter = null!;

    public TokenStockState State { get; private set; } = TokenStockState.Unknown;

    public override void _Ready()
    {
        _stateLabel = GetNode<Label>("Margin/Content/AvailabilityRow/State");
        _meter = GetNode<ProgressBar>("Margin/Content/AvailabilityRow/Meter");
        _approximateCountLabel = GetNode<Label>("Margin/Content/AvailabilityRow/ApproximateCount");
        SetUnknown();
    }

    public void SetLevel(TokenStockState state) => SetLevel(state, string.Empty);

    public void SetLevel(TokenStockState state, string approximateCount)
    {
        State = state;
        _stateLabel.Text = state switch
        {
            TokenStockState.Empty => "НЕТ ЖЕТОНОВ",
            TokenStockState.Low => "МАЛО",
            TokenStockState.Enough => "ДОСТАТОЧНО",
            TokenStockState.Much => "МНОГО",
            _ => "НЕТ ДАННЫХ",
        };
        _approximateCountLabel.Text = approximateCount;
        _meter.Value = state switch
        {
            TokenStockState.Low => 25,
            TokenStockState.Enough => 50,
            TokenStockState.Much => 100,
            _ => 0,
        };
        _meter.AddThemeStyleboxOverride(
            "fill",
            CreateFillStyle(state == TokenStockState.Much
                ? new Color("58c84b")
                : new Color("d43d42")));
    }

    public void SetUnknown() => SetLevel(TokenStockState.Unknown);

    private static StyleBoxFlat CreateFillStyle(Color color) =>
        new()
        {
            BgColor = color,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        };
}
