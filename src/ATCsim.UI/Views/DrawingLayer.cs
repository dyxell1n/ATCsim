using System.Windows;
using System.Windows.Media;

namespace ATCsim.UI.Views;

public sealed class DrawingLayer : FrameworkElement
{
    public static readonly DependencyProperty DrawingProperty = DependencyProperty.Register(
        nameof(Drawing), typeof(Drawing), typeof(DrawingLayer),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public Drawing? Drawing
    {
        get => (Drawing?)GetValue(DrawingProperty);
        set => SetValue(DrawingProperty, value);
    }

    protected override void OnRender(DrawingContext context) => context.DrawDrawing(Drawing);
}
