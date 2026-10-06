using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ATCsim.UI.Views;

/// <summary>
/// Interaction logic for RadarCanvasView.xaml.
/// Implements smooth viewport panning via RMB drag and zooming via Mouse Wheel.
/// Pure presentation logic (zero business or flight kinematics calculations).
/// </summary>
public partial class RadarCanvasView : UserControl
{
    private Point _lastDragPoint;
    private bool _isDragging;
    private bool _initialFitApplied;

    public RadarCanvasView()
    {
        InitializeComponent();
    }

    private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_initialFitApplied && e.NewSize.Width > 100 && e.NewSize.Height > 100)
        {
            ResetViewToFit();
            _initialFitApplied = true;
        }
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        Point mousePos = e.GetPosition(ViewportContainer);

        double zoomFactor = e.Delta > 0 ? 1.15 : (1.0 / 1.15);
        double currentScale = MapScaleTransform.ScaleX;
        double newScale = Math.Clamp(currentScale * zoomFactor, 0.4, 4.5);

        if (Math.Abs(newScale - currentScale) < 0.001)
            return;

        // Zoom centered around current mouse cursor position
        double scaleRatio = newScale / currentScale;
        double newTransX = mousePos.X - (mousePos.X - MapTranslateTransform.X) * scaleRatio;
        double newTransY = mousePos.Y - (mousePos.Y - MapTranslateTransform.Y) * scaleRatio;

        MapScaleTransform.ScaleX = newScale;
        MapScaleTransform.ScaleY = newScale;
        MapTranslateTransform.X = newTransX;
        MapTranslateTransform.Y = newTransY;

        e.Handled = true;
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _lastDragPoint = e.GetPosition(ViewportContainer);
        ViewportContainer.CaptureMouse();
        ViewportContainer.Cursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && e.RightButton == MouseButtonState.Pressed)
        {
            Point currentPoint = e.GetPosition(ViewportContainer);
            Vector delta = currentPoint - _lastDragPoint;

            MapTranslateTransform.X += delta.X;
            MapTranslateTransform.Y += delta.Y;

            _lastDragPoint = currentPoint;
            e.Handled = true;
        }
        else if (_isDragging && e.RightButton == MouseButtonState.Released)
        {
            EndDrag();
        }
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            EndDrag();
            e.Handled = true;
        }
    }

    private void EndDrag()
    {
        _isDragging = false;
        ViewportContainer.ReleaseMouseCapture();
        ViewportContainer.Cursor = Cursors.Arrow;
    }

    private void OnResetViewClick(object sender, RoutedEventArgs e)
    {
        ResetViewToFit();
    }

    private void ResetViewToFit()
    {
        double viewportW = ViewportContainer.ActualWidth;
        double viewportH = ViewportContainer.ActualHeight;

        if (viewportW <= 0 || viewportH <= 0)
        {
            viewportW = 750;
            viewportH = 500;
        }

        const double mapW = 1000.0;
        const double mapH = 700.0;

        // Scale to fit nicely with small margin
        double scaleX = viewportW / mapW;
        double scaleY = viewportH / mapH;
        double fitScale = Math.Min(scaleX, scaleY);

        MapScaleTransform.ScaleX = fitScale;
        MapScaleTransform.ScaleY = fitScale;

        // Center map in viewport
        MapTranslateTransform.X = (viewportW - mapW * fitScale) / 2.0;
        MapTranslateTransform.Y = (viewportH - mapH * fitScale) / 2.0;
    }
}
