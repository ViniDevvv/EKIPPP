using System.Windows;
using System.Windows.Controls;

namespace EKIPPP.Helpers;

public static class PanelSpacing
{
    public static readonly DependencyProperty SpacingProperty =
        DependencyProperty.RegisterAttached("Spacing", typeof(double), typeof(PanelSpacing),
            new PropertyMetadata(0.0, OnSpacingChanged));

    public static double GetSpacing(DependencyObject obj) => (double)obj.GetValue(SpacingProperty);
    public static void SetSpacing(DependencyObject obj, double value) => obj.SetValue(SpacingProperty, value);

    private static void OnSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StackPanel sp)
        {
            sp.Loaded -= ApplyOnLoaded;
            sp.Loaded += ApplyOnLoaded;
        }
    }

    private static void ApplyOnLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not StackPanel sp) return;
        double spacing = GetSpacing(sp);
        bool first = true;
        foreach (UIElement child in sp.Children)
        {
            if (child is not FrameworkElement fe) continue;
            if (first) { first = false; continue; }
            fe.Margin = sp.Orientation == Orientation.Vertical
                ? new Thickness(fe.Margin.Left, spacing, fe.Margin.Right, fe.Margin.Bottom)
                : new Thickness(spacing, fe.Margin.Top, fe.Margin.Right, fe.Margin.Bottom);
        }
    }
}
