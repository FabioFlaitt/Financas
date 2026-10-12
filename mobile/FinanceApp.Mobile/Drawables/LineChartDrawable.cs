using FinanceApp.Mobile.Models;

namespace FinanceApp.Mobile.Drawables;

/// <summary>Gráfico de linha/área da evolução diária do saldo no mês.</summary>
public class LineChartDrawable : IDrawable
{
    private readonly IReadOnlyList<DailyBalancePoint> _points;
    private readonly Color _lineColor;

    public LineChartDrawable(IReadOnlyList<DailyBalancePoint> points, Color lineColor)
    {
        _points = points;
        _lineColor = lineColor;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (_points.Count < 2)
        {
            canvas.FontColor = Color.FromArgb("#919191");
            canvas.FontSize = 13;
            canvas.DrawString("Dados insuficientes", dirtyRect.Center.X, dirtyRect.Center.Y, HorizontalAlignment.Center);
            return;
        }

        const float padLeft = 8f, padRight = 8f, padTop = 10f, padBottom = 18f;
        var plotW = dirtyRect.Width - padLeft - padRight;
        var plotH = dirtyRect.Height - padTop - padBottom;

        var min = (float)_points.Min(p => p.Balance);
        var max = (float)_points.Max(p => p.Balance);
        if (Math.Abs(max - min) < 0.01f) { max += 1; min -= 1; }
        var range = max - min;

        float X(int i) => padLeft + plotW * i / (_points.Count - 1);
        float Y(decimal v) => padTop + plotH * (1f - ((float)v - min) / range);

        // Linha de base zero (se dentro do intervalo)
        if (min < 0 && max > 0)
        {
            canvas.StrokeColor = Color.FromArgb("#E1E1E1");
            canvas.StrokeSize = 1;
            var zeroY = Y(0);
            canvas.DrawLine(padLeft, zeroY, padLeft + plotW, zeroY);
        }

        // Área sob a curva
        var area = new PathF();
        area.MoveTo(X(0), padTop + plotH);
        for (int i = 0; i < _points.Count; i++)
            area.LineTo(X(i), Y(_points[i].Balance));
        area.LineTo(X(_points.Count - 1), padTop + plotH);
        area.Close();
        canvas.FillColor = _lineColor.WithAlpha(0.15f);
        canvas.FillPath(area);

        // Linha
        var line = new PathF();
        line.MoveTo(X(0), Y(_points[0].Balance));
        for (int i = 1; i < _points.Count; i++)
            line.LineTo(X(i), Y(_points[i].Balance));
        canvas.StrokeColor = _lineColor;
        canvas.StrokeSize = 2.5f;
        canvas.StrokeLineJoin = LineJoin.Round;
        canvas.DrawPath(line);

        // Ponto final destacado
        var lastX = X(_points.Count - 1);
        var lastY = Y(_points[^1].Balance);
        canvas.FillColor = _lineColor;
        canvas.FillCircle(lastX, lastY, 3.5f);
    }
}
