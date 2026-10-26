using System.Globalization;
using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Text;
using Android.Text.Method;
using Android.Views;
using Android.Widget;
using Financeiro.Mobile.UI;
using FinanceiroApp.Helpers;
using FinanceiroApp.Models;

namespace Financeiro.Mobile.Forms;

internal sealed class EntryForm
{
    private const int Match = ViewGroup.LayoutParams.MatchParent;
    private const int Wrap = ViewGroup.LayoutParams.WrapContent;
    private readonly MobileUi _ui;
    private readonly Action _onSaved;
    private readonly Dialog _dialog;
    private readonly LinearLayout _root;
    private readonly LinearLayout _fields;
    private readonly ScrollView _scroll;
    private readonly TextView _error;
    private bool _busy;
    private bool _shown;

    public EntryForm(MobileUi ui, Action onSaved)
    {
        _ui = ui;
        _onSaved = onSaved;
        _dialog = new Dialog(ui.Context, Android.Resource.Style.ThemeMaterialLightNoActionBar);
        _dialog.SetCanceledOnTouchOutside(false);
        _root = ui.Column();
        _root.SetBackgroundColor(ui.BackgroundColor);
        _root.FocusableInTouchMode = true;
        _fields = ui.Column(16);
        _scroll = new ScrollView(_dialog.Context) { FillViewport = true };
        _scroll.SetClipToPadding(false);
        _error = ui.Text("", 14, ui.Danger);
        _error.AccessibilityLiveRegion = AccessibilityLiveRegion.Polite;
        _error.Visibility = ViewStates.Gone;
        _fields.AddView(_error, new LinearLayout.LayoutParams(Match, Wrap) { BottomMargin = ui.Dp(16) });
    }

    public EditText Text(string label, string value = "", bool multiline = false)
    {
        var input = new EditText(_dialog.Context)
        {
            Text = value,
            TextSize = 16,
            InputType = InputTypes.ClassText | (multiline ? InputTypes.TextFlagMultiLine : InputTypes.TextFlagCapSentences),
            Gravity = GravityFlags.Top | GravityFlags.Start,
            Background = FieldBackground(),
            BackgroundTintList = null,
            ContentDescription = label
        };
        input.SetTextColor(_ui.Ink);
        input.SetHintTextColor(_ui.Muted);
        input.SetPadding(_ui.Dp(12), _ui.Dp(12), _ui.Dp(12), _ui.Dp(12));
        input.SetMinHeight(_ui.Dp(48));
        input.SetSingleLine(!multiline);
        input.SetSelectAllOnFocus(false);
        if (multiline) input.SetMinLines(3);
        input.ImeOptions = multiline ? Android.Views.InputMethods.ImeAction.None : Android.Views.InputMethods.ImeAction.Next;
        AddField(label, input);
        return input;
    }

    public MoneyField Money(string label, decimal value = 0)
    {
        var input = Text(label, value.ToString("N2", FinancialFormatting.BrazilianCulture));
        input.KeyListener = DigitsKeyListener.GetInstance("0123456789.,+-");
        input.SetRawInputType(InputTypes.ClassNumber | InputTypes.NumberFlagDecimal | InputTypes.NumberFlagSigned);
        return new MoneyField(input, label);
    }

    public EditText Password(string label, bool numeric = false)
    {
        var input = Text(label);
        input.InputType = numeric ? InputTypes.ClassNumber | InputTypes.NumberVariationPassword : InputTypes.ClassText | InputTypes.TextVariationPassword;
        input.TransformationMethod = PasswordTransformationMethod.Instance;
        return input;
    }

    public IntegerField Integer(string label, int value = 1)
    {
        var input = Text(label, value.ToString(FinancialFormatting.BrazilianCulture));
        input.InputType = InputTypes.ClassNumber | InputTypes.NumberFlagSigned;
        return new IntegerField(input, label);
    }

    public CheckBox Check(string label, bool value)
    {
        var input = new CheckBox(_dialog.Context)
        {
            Text = label,
            TextSize = 16,
            Checked = value,
            ButtonTintList = new ColorStateList(
                new[] { new[] { Android.Resource.Attribute.StateChecked }, Array.Empty<int>() },
                new[] { _ui.Accent.ToArgb(), _ui.Muted.ToArgb() })
        };
        input.SetTextColor(_ui.Ink);
        input.SetMinHeight(_ui.Dp(48));
        _fields.AddView(input, new LinearLayout.LayoutParams(Match, Wrap) { BottomMargin = _ui.Dp(12) });
        return input;
    }

    public SelectionField<string> Choice(string label, IEnumerable<string> choices, string selected)
    {
        var values = choices.Distinct().ToList();
        if (!string.IsNullOrEmpty(selected) && !values.Contains(selected)) values.Add(selected);
        if (values.Count == 0) values.Add("");
        return Select(label, values, values, Math.Max(0, values.IndexOf(selected)));
    }

    public SelectionField<LookupItem> Lookup(string label, IEnumerable<LookupItem> choices, string selectedName = "", int selectedId = 0)
    {
        var values = choices.ToList();
        if (values.All(item => item.Id != 0)) values.Insert(0, new LookupItem(0, "Selecione"));
        var selected = selectedId > 0
            ? values.FindIndex(item => item.Id == selectedId)
            : values.FindIndex(item => item.Id > 0 && item.Name == selectedName);
        return Select(label, values, values.Select(item => item.Name).ToList(), Math.Max(0, selected));
    }

    public DateField Date(string label, DateTime value, bool monthOnly = false)
    {
        var button = _ui.Button("", () => { }, false);
        button.Gravity = GravityFlags.Start | GravityFlags.CenterVertical;
        button.Background = FieldBackground();
        button.ContentDescription = label;
        var container = AddField(label, button);
        var field = new DateField(button, container, value, monthOnly);
        button.Click += (_, _) =>
        {
            var current = field.Value;
            var picker = new DatePickerDialog(
                _dialog.Context,
                _ui.Dark ? Android.Resource.Style.ThemeMaterialDialogAlert : Android.Resource.Style.ThemeMaterialLightDialogAlert,
                (_, args) => field.Value = args.Date,
                current.Year, current.Month - 1, current.Day);
            picker.Show();
            picker.GetButton((int)DialogButtonType.Positive)?.SetTextColor(_ui.Accent);
            picker.GetButton((int)DialogButtonType.Negative)?.SetTextColor(_ui.Accent);
        };
        return field;
    }

    public void ReadOnly(string label, string value)
    {
        var text = _ui.Text(value, 16);
        text.SetTextIsSelectable(true);
        AddField(label, text);
    }

    public void Show(string title, Action save, Action? delete = null)
    {
        if (_shown) throw new InvalidOperationException("O formulário já está aberto.");
        _shown = true;
        _dialog.SetTitle(title);
        var heading = _ui.Text(title, 22, bold: true);
        heading.SetPadding(_ui.Dp(16), _ui.Dp(20), _ui.Dp(16), _ui.Dp(16));
        if (OperatingSystem.IsAndroidVersionAtLeast(28)) heading.AccessibilityHeading = true;
        _root.AddView(heading, new LinearLayout.LayoutParams(Match, Wrap));
        _scroll.AddView(_fields, new ScrollView.LayoutParams(Match, Wrap));
        _root.AddView(_scroll, new LinearLayout.LayoutParams(Match, 0, 1));

        var actions = _ui.Column(16);
        actions.SetBackgroundColor(_ui.Surface);
        var buttons = new LinearLayout(_dialog.Context) { Orientation = Android.Widget.Orientation.Horizontal };
        var cancel = _ui.Button("Cancelar", () => _dialog.Dismiss(), false);
        var submit = _ui.Button("Salvar", () => Run(save));
        buttons.AddView(cancel, new LinearLayout.LayoutParams(0, Wrap, 1) { RightMargin = _ui.Dp(8) });
        buttons.AddView(submit, new LinearLayout.LayoutParams(0, Wrap, 1));
        actions.AddView(buttons, new LinearLayout.LayoutParams(Match, Wrap));
        if (delete is not null)
        {
            var remove = _ui.Button("Excluir registro", () => ConfirmDelete(delete), false);
            remove.SetTextColor(_ui.Danger);
            _fields.AddView(remove, new LinearLayout.LayoutParams(Match, Wrap) { TopMargin = _ui.Dp(8) });
        }
        _root.AddView(actions, new LinearLayout.LayoutParams(Match, Wrap));

        _dialog.SetContentView(_root);
        var window = _dialog.Window!;
        window.SetBackgroundDrawable(_ui.Background(_ui.BackgroundColor, 0));
        window.SetSoftInputMode(SoftInput.AdjustResize | SoftInput.StateAlwaysHidden);
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            window.SetDecorFitsSystemWindows(false);
            _root.SetOnApplyWindowInsetsListener(new FormInsetsListener());
        }
        else
        {
            _root.SetFitsSystemWindows(true);
        }
#pragma warning disable CS0618
        window.SetStatusBarColor(_ui.BackgroundColor);
        window.SetNavigationBarColor(OperatingSystem.IsAndroidVersionAtLeast(26) ? _ui.Surface : Color.Black);
        var systemUi = _ui.Dark ? (SystemUiFlags)0 : SystemUiFlags.LightStatusBar;
        if (!_ui.Dark && OperatingSystem.IsAndroidVersionAtLeast(26)) systemUi |= SystemUiFlags.LightNavigationBar;
        window.DecorView.SystemUiVisibility = (StatusBarVisibility)systemUi;
#pragma warning restore CS0618
        _dialog.Show();
        window.SetLayout(Match, Match);
        _root.RequestApplyInsets();
        _root.RequestFocus();
    }

    private Android.Graphics.Drawables.GradientDrawable FieldBackground()
    {
        var background = _ui.Background(_ui.Surface);
        background.SetStroke(_ui.Dp(1), _ui.Line);
        return background;
    }

    private LinearLayout AddField(string label, View input)
    {
        var container = _ui.Column();
        var caption = _ui.Text(label, 13, _ui.Muted, true);
        input.Id = View.GenerateViewId();
        caption.LabelFor = input.Id;
        container.AddView(caption, new LinearLayout.LayoutParams(Match, Wrap) { BottomMargin = _ui.Dp(6) });
        container.AddView(input, new LinearLayout.LayoutParams(Match, Wrap));
        _fields.AddView(container, new LinearLayout.LayoutParams(Match, Wrap) { BottomMargin = _ui.Dp(16) });
        return container;
    }

    private SelectionField<T> Select<T>(string label, IReadOnlyList<T> values, IList<string> labels, int selected)
    {
        var spinner = new Spinner(_dialog.Context, SpinnerMode.Dropdown)
        {
            Adapter = new FieldAdapter(_ui, labels),
            ContentDescription = label,
            BackgroundTintList = ColorStateList.ValueOf(_ui.Accent)
        };
        spinner.SetPopupBackgroundDrawable(_ui.Background(_ui.Surface));
        spinner.SetMinimumHeight(_ui.Dp(48));
        spinner.SetSelection(selected);
        AddField(label, spinner);
        return new SelectionField<T>(spinner, values);
    }

    private void ConfirmDelete(Action delete)
    {
        var confirmation = new AlertDialog.Builder(_dialog.Context,
                _ui.Dark ? Android.Resource.Style.ThemeMaterialDialogAlert : Android.Resource.Style.ThemeMaterialLightDialogAlert)
            .SetTitle("Excluir registro?")
            .SetMessage("Esta exclusão não pode ser desfeita. Deseja continuar?")
            .SetNegativeButton("Cancelar", (_, _) => { })
            .SetPositiveButton("Excluir", (_, _) => Run(delete))
            .Create();
        confirmation.Show();
        confirmation.GetButton((int)DialogButtonType.Positive)?.SetTextColor(_ui.Danger);
        confirmation.GetButton((int)DialogButtonType.Negative)?.SetTextColor(_ui.Accent);
    }

    private void Run(Action operation)
    {
        if (_busy) return;
        _busy = true;
        _error.Visibility = ViewStates.Gone;
        try
        {
            operation();
        }
        catch (Exception ex)
        {
            _error.Text = ex.Message;
            _error.Visibility = ViewStates.Visible;
            _scroll.Post(() => _scroll.SmoothScrollTo(0, 0));
            return;
        }
        finally
        {
            _busy = false;
        }
        _dialog.Dismiss();
        _onSaved();
    }

    // Both spinner surfaces need explicit colors; the dialog intentionally uses a light base theme.
    private sealed class FieldAdapter(MobileUi ui, IList<string> labels)
        : ArrayAdapter<string>(ui.Context, Android.Resource.Layout.SimpleSpinnerItem, labels)
    {
        public override View GetView(int position, View? convertView, ViewGroup parent) => Label(position, false);
        public override View GetDropDownView(int position, View? convertView, ViewGroup? parent) => Label(position, true);

        private TextView Label(int position, bool dropdown)
        {
            var text = ui.Text(GetItem(position) ?? "", 16, ui.Ink);
            text.Gravity = GravityFlags.CenterVertical | GravityFlags.Start;
            text.SetPadding(ui.Dp(12), ui.Dp(12), ui.Dp(12), ui.Dp(12));
            text.SetMinHeight(ui.Dp(48));
            text.SetBackgroundColor(dropdown ? ui.Surface : Color.Transparent);
            return text;
        }
    }

    private sealed class FormInsetsListener : Java.Lang.Object, View.IOnApplyWindowInsetsListener
    {
        public WindowInsets OnApplyWindowInsets(View? view, WindowInsets? insets)
        {
            if (view is not null && insets is not null && OperatingSystem.IsAndroidVersionAtLeast(30))
            {
                var safe = insets.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.DisplayCutout() | WindowInsets.Type.Ime());
                view.SetPadding(safe.Left, safe.Top, safe.Right, safe.Bottom);
            }
            return insets!;
        }
    }

    internal sealed class MoneyField(EditText input, string label)
    {
        public decimal Value => FinancialFormatting.ParseMoney(input.Text ?? "", label);
    }

    internal sealed class IntegerField(EditText input, string label)
    {
        public int Value => int.TryParse(input.Text, NumberStyles.Integer, FinancialFormatting.BrazilianCulture, out var value)
            ? value
            : throw new InvalidOperationException($"Informe um número inteiro para {label.ToLower(FinancialFormatting.BrazilianCulture)}.");
    }

    internal sealed class SelectionField<T>(Spinner control, IReadOnlyList<T> values)
    {
        public T Value => values[Math.Clamp(control.SelectedItemPosition, 0, values.Count - 1)];
    }

    internal sealed class DateField
    {
        private readonly Button _button;
        private readonly LinearLayout _container;
        private readonly bool _monthOnly;
        private DateTime _value;

        public DateField(Button button, LinearLayout container, DateTime value, bool monthOnly)
        {
            _button = button;
            _container = container;
            _monthOnly = monthOnly;
            Value = value;
        }

        public DateTime Value
        {
            get => _value;
            set
            {
                _value = _monthOnly ? new DateTime(value.Year, value.Month, 1) : value.Date;
                _button.Text = _value.ToString(_monthOnly ? "MM/yyyy" : "dd/MM/yyyy", FinancialFormatting.BrazilianCulture);
            }
        }

        public bool Visible
        {
            set => _container.Visibility = value ? ViewStates.Visible : ViewStates.Gone;
        }
    }
}
