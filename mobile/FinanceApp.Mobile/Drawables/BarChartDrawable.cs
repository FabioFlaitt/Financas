using FinanceApp.Mobile.Models;

namespace FinanceApp.Mobile.Drawables;

/// <summary>
/// Barras agrupadas de receitas (verde) e despesas (vermelho) por mês,
/// usado na aba de gráficos para comparar a evolução ao longo dos meses.
/// </summary>
public class BarChartDrawable : IDrawable
{
    private readonly IReadOnlyList<MonthlyPoint> _points;
    private static readonly Color Income = Color.FromArgb("#2E7D32");
    private static readonly Color Expense = Color.FromArgb("#C62828");
    private static readonly Color Axis = Color.FromArgb("#E1E1E1");
    private static readonly Color LabelColor = Color.FromArgb("#919191");

    public BarChartDrawable(IReadOnlyList<MonthlyPoint> points) => _points = points;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (_points.Count == 0)
        {
            canvas.FontColor = LabelColor;
            canvas.FontSize = 13;
            canvas.DrawString("Sem dados", dirtyRect.Center.X, dirtyRect.Center.Y, HorizontalAlignment.Center);
            return;
        }

        const float padTop = 10f, padBottom = 20f, padSide = 6f;
        var plotW = dirtyRect.Width - padSide * 2;
        var plotH = dirtyRect.Height - padTop - padBottom;
        var baseY = padTop + plotH;

        var max = (float)Math.Max(_points.Max(p => p.Income), _points.Max(p => p.Expense));
        if (max <= 0) max = 1;

        // Eixo base
        canvas.StrokeColor = Axis;
        canvas.StrokeSize = 1;
        canvas.DrawLine(padSide, baseY, padSide + plotW, baseY);

        var slot = plotW / _points.Count;
        var barW = Math.Min(16f, slot * 0.30f);
        var gap = 2f;

        for (int i = 0; i < _points.Count; i++)
        {
            var p = _points[i];
            var center = padSide + slot * i + slot / 2f;

            var incH = plotH * (float)p.Income / max;
            var expH = plotH * (float)p.Expense / max;

            canvas.FillColor = Income;
            FillRoundedBar(canvas, center - barW - gap / 2f, baseY - incH, barW, incH);

            canvas.FillColor = Expense;
            FillRoundedBar(canvas, center + gap / 2f, baseY - expH, barW, expH);

            canvas.FontColor = LabelColor;
            canvas.FontSize = 9;
            canvas.DrawString(p.Label, center, baseY + 13, HorizontalAlignment.Center);
        }
    }

    private static void FillRoundedBar(ICanvas canvas, float x, float y, float w, float h)
    {
        if (h <= 0) return;
        var radius = Math.Min(3f, w / 2f);
        canvas.FillRoundedRectangle(x, y, w, Math.Max(h, 1f), radius);
    }
}
