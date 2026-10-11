using System.Windows;
using System.Windows.Controls;
namespace BT583Printer;
/// <summary>Shared themed modal form with actions always visible below scrollable fields.</summary>
public sealed class FormDialog : Window
{
    private readonly StackPanel panel = new() { Margin = new Thickness(24, 4, 24, 16) };
    private readonly DockPanel layout = new();
    public FormDialog(Window owner, string title)
    {
        Owner = owner;
        Title = title;
        Width = 520;
        SizeToContent = SizeToContent.Height;
        MaxHeight = Math.Max(500, Math.Min(800, owner.ActualHeight - 50));
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        FontFamily = owner.FontFamily;
        FontSize = 13;
        Resources.MergedDictionaries.Add(owner.Resources);
        SetResourceReference(BackgroundProperty, "Card");
        SetResourceReference(ForegroundProperty, "Ink");
        var heading = new TextBlock { Text = title, FontSize = 22, FontWeight = FontWeights.SemiBold, Margin = new Thickness(24, 20, 24, 12) };
        DockPanel.SetDock(heading, Dock.Top);
        layout.Children.Add(heading);
        Content = layout;
    }
    public TextBox Text(string label, string value)
    {
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 10, 0, 6) });
        var box = new TextBox { Text = value, MinWidth = 350 };
        panel.Children.Add(box);
        return box;
    }
    public ComboBox Choice(string label, IEnumerable<string> choices, string selected)
    {
        panel.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 10, 0, 6) });
        var box = new ComboBox { ItemsSource = choices.ToArray(), SelectedItem = selected, Margin = new Thickness(0), MinHeight = 36 };
        if (box.SelectedIndex < 0)
            box.SelectedIndex = 0;
        panel.Children.Add(box);
        return box;
    }
    public CheckBox Check(string label, bool value)
    {
        var box = new CheckBox { Content = new TextBlock { Text = label, TextWrapping = TextWrapping.Wrap, MaxWidth = 425 }, IsChecked = value, Margin = new Thickness(0, 14, 0, 4) };
        panel.Children.Add(box);
        return box;
    }
    public void Note(string text)
    {
        var note = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 4), FontSize = 12 };
        note.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        panel.Children.Add(note);
    }
    public bool Accept(string caption = "Save")
    {
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(24, 14, 24, 14) };
        var ok = new Button { Content = caption, IsDefault = true, Padding = new Thickness(22, 10, 22, 10), Foreground = System.Windows.Media.Brushes.White };
        ok.SetResourceReference(BackgroundProperty, "Accent");
        ok.SetResourceReference(BorderBrushProperty, "Accent");
        ok.Click += (_, _) => DialogResult = true;
        var cancel = new Button { Content = "Cancel", IsCancel = true, Padding = new Thickness(18, 10, 18, 10), Margin = new Thickness(8, 0, 0, 6) };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        DockPanel.SetDock(buttons, Dock.Bottom);
        layout.Children.Add(buttons);
        layout.Children.Add(new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        return ShowDialog() == true;
    }
}
