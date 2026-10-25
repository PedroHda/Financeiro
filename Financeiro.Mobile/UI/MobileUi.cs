using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;

namespace Financeiro.Mobile.UI;

public sealed class MobileUi
{
    private readonly Typeface _icons;
    public Context Context { get; }
    public bool Dark { get; }
    public Color BackgroundColor => Color.ParseColor(Dark ? "#101A17" : "#F5F7F6");
    public Color Surface => Color.ParseColor(Dark ? "#192720" : "#FFFFFF");
    public Color Ink => Color.ParseColor(Dark ? "#EBF3EE" : "#192C24");
    public Color Muted => Color.ParseColor(Dark ? "#A7BAAE" : "#65796D");
    public Color Accent => Color.ParseColor(Dark ? "#6EE7AF" : "#087F5B");
    public Color Danger => Color.ParseColor(Dark ? "#FF9797" : "#BA3343");
    public Color Warning => Color.ParseColor(Dark ? "#F5CC7C" : "#98621A");
    public Color Line => Color.ParseColor(Dark ? "#304439" : "#DFE8E2");
    public Color Soft => Color.ParseColor(Dark ? "#243C2E" : "#E9F5EE");

    public MobileUi(Context context, bool dark)
    {
        Context = context;
        Dark = dark;
        _icons = Typeface.CreateFromAsset(context.Assets, "MaterialIcons.ttf")!;
    }

    public int Dp(float value) => (int)(value * Context.Resources!.DisplayMetrics!.Density + 0.5f);

    public LinearLayout Column(int padding = 0)
    {
        var layout = new LinearLayout(Context) { Orientation = Android.Widget.Orientation.Vertical };
        layout.SetPadding(Dp(padding), Dp(padding), Dp(padding), Dp(padding));
        return layout;
    }

    public LinearLayout Row()
    {
        var row = new LinearLayout(Context) { Orientation = Android.Widget.Orientation.Horizontal };
        row.SetGravity(GravityFlags.CenterVertical);
        return row;
    }

    public TextView Text(string text, float size = 14, Color? color = null, bool bold = false)
    {
        var label = new TextView(Context) { Text = text, TextSize = size };
        label.SetIncludeFontPadding(false);
        label.SetTextColor(color ?? Ink);
        label.SetTypeface(Typeface.Create(bold ? "sans-serif-medium" : "sans-serif", TypefaceStyle.Normal), TypefaceStyle.Normal);
        return label;
    }

    public TextView Icon(string glyph, Color? color = null, int size = 24)
    {
        var icon = Text(glyph, size, color);
        icon.Typeface = _icons;
        icon.Gravity = GravityFlags.Center;
        icon.ImportantForAccessibility = ImportantForAccessibility.No;
        return icon;
    }

    public GradientDrawable Background(Color color, int radius = 8)
    {
        var drawable = new GradientDrawable();
        drawable.SetColor(color);
        drawable.SetCornerRadius(Dp(radius));
        return drawable;
    }

    public Button Button(string text, Action click, bool primary = true)
    {
        var button = new Button(Context)
        {
            Text = text,
            TextSize = 14,
            Background = Background(primary ? Accent : Surface),
            StateListAnimator = null,
            ContentDescription = text,
            Gravity = GravityFlags.Center
        };
        button.SetAllCaps(false);
        button.SetMinHeight(Dp(48));
        button.SetTextColor(primary ? (Dark ? Color.ParseColor("#103426") : Color.White) : Ink);
        button.SetPadding(Dp(16), Dp(6), Dp(16), Dp(6));
        button.Click += (_, _) => click();
        return button;
    }

    public TextView IconButton(string glyph, string description, Action click)
    {
        var button = Icon(glyph);
        button.ImportantForAccessibility = ImportantForAccessibility.Yes;
        button.ContentDescription = description;
        button.Focusable = true;
        if (OperatingSystem.IsAndroidVersionAtLeast(26)) button.TooltipText = description;
        button.Background = Background(Soft);
        button.LayoutParameters = new LinearLayout.LayoutParams(Dp(44), Dp(44));
        button.Click += (_, _) => click();
        return button;
    }

    public LinearLayout Card(int padding = 16)
    {
        var card = Column(padding);
        var background = Background(Surface);
        background.SetStroke(Dp(1), Line);
        card.Background = background;
        card.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
        {
            BottomMargin = Dp(12)
        };
        return card;
    }

    public void Gap(LinearLayout parent, int size = 12) => parent.AddView(new View(Context), new LinearLayout.LayoutParams(1, Dp(size)));

    public void Divider(LinearLayout parent)
    {
        var line = new View(Context);
        line.SetBackgroundColor(Line);
        parent.AddView(line, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(1)) { TopMargin = Dp(10), BottomMargin = Dp(10) });
    }

    public void Progress(LinearLayout parent, decimal percentage, Color? color = null)
    {
        var progress = new ProgressBar(Context, null, Android.Resource.Attribute.ProgressBarStyleHorizontal)
        {
            Max = 1000,
            Progress = (int)Math.Clamp(percentage * 10, 0, 1000),
            ProgressTintList = ColorStateList.ValueOf(color ?? Accent),
            ProgressBackgroundTintList = ColorStateList.ValueOf(Line)
        };
        parent.AddView(progress, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, Dp(8)) { TopMargin = Dp(10), BottomMargin = Dp(6) });
    }

    public static LinearLayout.LayoutParams Grow() => new(0, ViewGroup.LayoutParams.WrapContent, 1);
}

internal sealed class InsetsListener(Action<int, int> apply) : Java.Lang.Object, View.IOnApplyWindowInsetsListener
{
    public WindowInsets OnApplyWindowInsets(View? view, WindowInsets? insets)
    {
        if (insets is null) return null!;
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            var bars = insets.GetInsets(WindowInsets.Type.SystemBars());
            apply(bars.Top, bars.Bottom);
        }
        else
        {
#pragma warning disable CS0618
            apply(insets.SystemWindowInsetTop, insets.SystemWindowInsetBottom);
#pragma warning restore CS0618
        }
        return insets;
    }
}

public sealed class MobileChoiceAdapter(MobileUi ui, string[] items)
    : ArrayAdapter<string>(ui.Context, Android.Resource.Layout.SimpleSpinnerItem, items)
{
    public override View GetView(int position, View? convertView, ViewGroup parent)
    {
        return Label(base.GetView(position, convertView, parent), false);
    }

    public override View GetDropDownView(int position, View? convertView, ViewGroup? parent)
    {
        return Label(base.GetDropDownView(position, convertView, parent)!, true);
    }

    private View Label(View view, bool dropdown)
    {
        if (view is TextView label)
        {
            label.SetTextColor(ui.Ink);
            label.SetBackgroundColor(dropdown ? ui.Surface : ui.Soft);
            label.TextSize = 12;
            label.SetSingleLine(false);
            label.SetMaxLines(2);
            label.SetPadding(ui.Dp(10), ui.Dp(12), ui.Dp(10), ui.Dp(12));
        }
        return view;
    }
}
