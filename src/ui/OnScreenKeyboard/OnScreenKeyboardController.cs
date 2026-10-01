using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Exchanger.UI.OnScreenKeyboard;

/// <summary>
/// Связывает редактируемые поля с авторскими узлами темы. Контроллер не создаёт,
/// не перемещает и не стилизует визуальные элементы клавиатуры.
/// </summary>
public sealed class OnScreenKeyboardController : IDisposable
{
    private readonly OnScreenKeyboardView _view;
    private readonly ScrollContainer _scroll;
    private readonly OnScreenKeyboardState _state = new();
    private readonly List<InputRegistration> _registrations = new();
    private readonly List<Action> _viewUnsubscribe = new();
    private LineEdit? _activeEdit;
    private SpinBox? _activeSpinBox;
    private bool _disposed;

    public OnScreenKeyboardController(OnScreenKeyboardView view, ScrollContainer scroll)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _scroll = scroll ?? throw new ArgumentNullException(nameof(scroll));
        _view.Validate();
        ConfigureViewSignals();
        HidePanels();
        RefreshTextKeys();
    }

    public bool IsVisible => _view.TextKeyboard.Visible || _view.NumericKeyboard.Visible;

    public event Action<bool>? VisibilityChanged;

    public void RegisterTextField(LineEdit edit) => Register(edit, null);

    public void RegisterNumericField(SpinBox spinBox)
    {
        ArgumentNullException.ThrowIfNull(spinBox);
        Register(spinBox.GetLineEdit(), spinBox);
    }

    public void ShowNumericField(SpinBox spinBox)
    {
        ArgumentNullException.ThrowIfNull(spinBox);
        ShowFor(spinBox.GetLineEdit(), spinBox);
    }

    public void ClearRegisteredFields()
    {
        CommitActiveSpinBox();
        foreach (InputRegistration registration in _registrations)
        {
            registration.Unsubscribe();
        }

        _registrations.Clear();
        _activeEdit = null;
        _activeSpinBox = null;
        HidePanels();
    }

    public void Hide(bool releaseFocus = true)
    {
        CommitActiveSpinBox();
        HidePanels();

        if (releaseFocus && GodotObject.IsInstanceValid(_activeEdit))
        {
            _activeEdit!.Unedit();
            _activeEdit.ReleaseFocus();
        }

        _activeEdit = null;
        _activeSpinBox = null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ClearRegisteredFields();
        foreach (Action unsubscribe in _viewUnsubscribe)
        {
            unsubscribe();
        }

        _viewUnsubscribe.Clear();
    }

    private void Register(LineEdit edit, SpinBox? spinBox)
    {
        ArgumentNullException.ThrowIfNull(edit);
        if (!edit.Editable || _registrations.Exists(item => ReferenceEquals(item.Edit, edit)))
        {
            return;
        }

        edit.VirtualKeyboardEnabled = false;
        edit.VirtualKeyboardShowOnFocus = false;

        Action focusEntered = () => ShowFor(edit, spinBox);
        Control.GuiInputEventHandler guiInput = input =>
        {
            if (input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }
                || input is InputEventScreenTouch { Pressed: true })
            {
                ShowFor(edit, spinBox);
            }
        };
        Action treeExiting = () => OnRegisteredFieldExiting(edit);
        edit.FocusEntered += focusEntered;
        edit.GuiInput += guiInput;
        edit.TreeExiting += treeExiting;
        _registrations.Add(new InputRegistration(edit, focusEntered, guiInput, treeExiting));
    }

    private void OnRegisteredFieldExiting(LineEdit edit)
    {
        if (ReferenceEquals(_activeEdit, edit))
        {
            Hide(releaseFocus: false);
        }
    }

    private void ShowFor(LineEdit edit, SpinBox? spinBox)
    {
        if (_disposed || !GodotObject.IsInstanceValid(edit) || !edit.Editable || !edit.IsVisibleInTree())
        {
            return;
        }

        if (!ReferenceEquals(_activeEdit, edit))
        {
            CommitActiveSpinBox();
        }

        _activeEdit = edit;
        _activeSpinBox = spinBox;
        edit.Edit();

        bool wasVisible = IsVisible;
        if (spinBox is null)
        {
            _state.UseTextLayout();
            RefreshTextKeys();
            _view.NumericKeyboard.Visible = false;
            _view.TextKeyboard.Visible = true;
        }
        else
        {
            _state.UseNumericLayout();
            _view.TextKeyboard.Visible = false;
            _view.NumericKeyboard.Visible = true;
        }

        if (!wasVisible && IsVisible)
        {
            VisibilityChanged?.Invoke(true);
        }

        ScrollFieldAboveKeyboard(edit, spinBox is null ? _view.TextKeyboard : _view.NumericKeyboard);
    }

    private async void ScrollFieldAboveKeyboard(Control field, Control keyboard)
    {
        await field.ToSignal(field.GetTree(), SceneTree.SignalName.ProcessFrame);
        if (_disposed || !GodotObject.IsInstanceValid(field) || !GodotObject.IsInstanceValid(keyboard))
        {
            return;
        }

        if (!_scroll.IsAncestorOf(field))
        {
            return;
        }

        _scroll.EnsureControlVisible(field);
        float keyboardTop = keyboard.GetGlobalRect().Position.Y;
        float fieldBottom = field.GetGlobalRect().End.Y;
        float overlap = fieldBottom + 20f - keyboardTop;
        if (overlap > 0f)
        {
            _scroll.ScrollVertical += Mathf.CeilToInt(overlap);
        }
    }

    private void ConfigureViewSignals()
    {
        Bind(_view.TextHide, () => Hide());
        Bind(_view.NumericHide, () => Hide());
        Bind(_view.Language, () =>
        {
            _state.ToggleLanguage();
            RefreshTextKeys();
        });
        Bind(_view.Symbols, () =>
        {
            _state.ToggleSymbols();
            RefreshTextKeys();
        });
        Bind(_view.Shift, () =>
        {
            _state.ToggleShift();
            RefreshTextKeys();
        });
        Bind(_view.TextBackspace, Backspace);
        Bind(_view.NumericBackspace, Backspace);
        Bind(_view.TextClear, Clear);
        Bind(_view.NumericClear, Clear);
        Bind(_view.Space, () => Insert(" "));
        Bind(_view.CaretLeft, () => MoveCaret(forward: false));
        Bind(_view.CaretRight, () => MoveCaret(forward: true));

        for (int index = 0; index < _view.TextKeys.Count; index++)
        {
            int keyIndex = index;
            Bind(_view.TextKeys[index], () => InsertVisibleTextKey(keyIndex));
        }

        for (int digit = 0; digit < _view.Digits.Count; digit++)
        {
            int value = digit;
            _view.Digits[digit].Text = digit.ToString();
            Bind(_view.Digits[digit], () => Insert(value.ToString()));
        }
    }

    private void Bind(Button button, Action handler)
    {
        button.Pressed += handler;
        _viewUnsubscribe.Add(() =>
        {
            if (GodotObject.IsInstanceValid(button))
            {
                button.Pressed -= handler;
            }
        });
    }

    private void InsertVisibleTextKey(int index)
    {
        IReadOnlyList<string> keys = _state.Keys;
        if (index < keys.Count)
        {
            Insert(keys[index]);
        }
    }

    private void Insert(string value)
    {
        if (!TryGetActiveEdit(out LineEdit edit))
        {
            return;
        }

        ApplyTextMutation(edit, () =>
        {
            DeleteSelection(edit);
            edit.InsertTextAtCaret(value);
        });
    }

    private void Backspace()
    {
        if (!TryGetActiveEdit(out LineEdit edit))
        {
            return;
        }

        if (edit.HasSelection())
        {
            ApplyTextMutation(edit, () => DeleteSelection(edit));
            return;
        }

        int caret = edit.CaretColumn;
        if (caret <= 0)
        {
            return;
        }

        int previous = edit.GetPreviousCompositeCharacterColumn(caret);
        ApplyTextMutation(edit, () =>
        {
            edit.DeleteText(previous, caret);
            edit.CaretColumn = previous;
        });
    }

    private void Clear()
    {
        if (!TryGetActiveEdit(out LineEdit edit))
        {
            return;
        }

        ApplyTextMutation(edit, edit.Clear);
    }

    private void MoveCaret(bool forward)
    {
        if (!TryGetActiveEdit(out LineEdit edit))
        {
            return;
        }

        if (edit.HasSelection())
        {
            edit.CaretColumn = forward
                ? edit.GetSelectionToColumn()
                : edit.GetSelectionFromColumn();
            edit.Deselect();
            return;
        }

        edit.CaretColumn = forward
            ? edit.GetNextCompositeCharacterColumn(edit.CaretColumn)
            : edit.GetPreviousCompositeCharacterColumn(edit.CaretColumn);
    }

    private static bool DeleteSelection(LineEdit edit)
    {
        if (!edit.HasSelection())
        {
            return false;
        }

        int from = edit.GetSelectionFromColumn();
        int to = edit.GetSelectionToColumn();
        edit.DeleteText(from, to);
        edit.CaretColumn = from;
        edit.Deselect();
        return true;
    }

    private void ApplyTextMutation(LineEdit edit, Action mutation)
    {
        string originalText = edit.Text;
        bool signalsWereBlocked = edit.IsBlockingSignals();
        if (!signalsWereBlocked)
        {
            edit.SetBlockSignals(true);
        }

        try
        {
            mutation();
        }
        finally
        {
            if (!signalsWereBlocked)
            {
                edit.SetBlockSignals(false);
            }
        }

        if (!string.Equals(originalText, edit.Text, StringComparison.Ordinal))
        {
            NotifyTextMutation(edit, emitSignal: !signalsWereBlocked);
        }
    }

    private void NotifyTextMutation(LineEdit edit, bool emitSignal = true)
    {
        if (emitSignal)
        {
            edit.EmitSignal(LineEdit.SignalName.TextChanged, edit.Text);
        }

        ApplyNumericValue();
    }

    private bool TryGetActiveEdit(out LineEdit edit)
    {
        if (GodotObject.IsInstanceValid(_activeEdit) && _activeEdit!.Editable)
        {
            edit = _activeEdit;
            return true;
        }

        edit = null!;
        HidePanels();
        return false;
    }

    private void ApplyNumericValue()
    {
        if (GodotObject.IsInstanceValid(_activeSpinBox)
            && GodotObject.IsInstanceValid(_activeEdit)
            && TryParseNumericText(_activeEdit!.Text))
        {
            _activeSpinBox!.Apply();
        }
    }

    private static bool TryParseNumericText(string text)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _)
           || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out _);

    private void CommitActiveSpinBox()
    {
        if (GodotObject.IsInstanceValid(_activeSpinBox))
        {
            _activeSpinBox!.Apply();
        }
    }

    private void RefreshTextKeys()
    {
        IReadOnlyList<string> keys = _state.Keys;
        for (int index = 0; index < _view.TextKeys.Count; index++)
        {
            Button button = _view.TextKeys[index];
            bool used = index < keys.Count;
            button.Visible = used;
            button.Disabled = !used;
            button.Text = used ? keys[index] : string.Empty;
        }

        bool alphabetic = _state.Layout is OnScreenKeyboardLayout.Russian or OnScreenKeyboardLayout.English;
        _view.Shift.Visible = alphabetic;
        _view.Shift.ButtonPressed = _state.Shift;
        _view.Language.Visible = alphabetic;
        _view.Language.Text = _state.Layout == OnScreenKeyboardLayout.Russian ? "EN" : "РУС";
        _view.Symbols.Text = _state.Layout == OnScreenKeyboardLayout.Symbols
            ? _state.AlphabeticLayout == OnScreenKeyboardLayout.Russian ? "АБВ" : "ABC"
            : "?123";
    }

    private void HidePanels()
    {
        bool wasVisible = IsVisible;
        if (GodotObject.IsInstanceValid(_view.TextKeyboard))
        {
            _view.TextKeyboard.Visible = false;
        }

        if (GodotObject.IsInstanceValid(_view.NumericKeyboard))
        {
            _view.NumericKeyboard.Visible = false;
        }

        if (wasVisible && !IsVisible)
        {
            VisibilityChanged?.Invoke(false);
        }
    }

    private sealed class InputRegistration(
        LineEdit edit,
        Action focusEntered,
        Control.GuiInputEventHandler guiInput,
        Action treeExiting)
    {
        public LineEdit Edit { get; } = edit;

        public void Unsubscribe()
        {
            if (!GodotObject.IsInstanceValid(Edit))
            {
                return;
            }

            Edit.FocusEntered -= focusEntered;
            Edit.GuiInput -= guiInput;
            Edit.TreeExiting -= treeExiting;
        }
    }
}
