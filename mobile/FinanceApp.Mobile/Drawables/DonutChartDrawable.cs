using FinanceApp.Mobile.Models;

namespace FinanceApp.Mobile.Drawables;

/// <summary>Gráfico de setores (rosca) da distribuição de despesas por categoria.</summary>
public class DonutChartDrawable : IDrawable
{
    private readonly IReadOnlyList<CategorySlice> _slices;
    private readonly Color _holeColor;

    public DonutChartDrawable(IReadOnlyList<CategorySlice> slices, Color holeColor)
    {
        _slices = slices;
        _holeColor = holeColor;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var total = _slices.Sum(s => s.Amount);
        if (total <= 0 || _slices.Count == 0)
        {
            DrawEmpty(canvas, dirtyRect);
            return;
        }

        var size = Math.Min(dirtyRect.Width, dirtyRect.Height);
        var r = size / 2f - 4f;
        var cx = dirtyRect.Center.X;
        var cy = dirtyRect.Center.Y;

        float start = 90f; // começa no topo
        foreach (var slice in _slices)
        {
            var sweep = (float)(slice.Amount / total * 360m);
            if (sweep <= 0) continue;
            canvas.FillColor = ParseColor(slice.ColorHex);
            // sentido horário: ângulo final = início - varredura
            canvas.FillArc(cx - r, cy - r, r * 2f, r * 2f, start, start - sweep, true);
            start -= sweep;
        }

        // Furo central (efeito rosca)
        canvas.FillColor = _holeColor;
        canvas.FillCircle(cx, cy, r * 0.58f);

        // Total no centro
        canvas.FontColor = ParseColor("#6E6E6E");
        canvas.FontSize = 12;
        canvas.DrawString("Total", cx, cy - 12, HorizontalAlignment.Center);
        canvas.FontColor = ParseColor("#212121");
        canvas.FontSize = 15;
        canvas.DrawString(total.ToString("C0", Converters.BrazilCulture.PtBr), cx, cy + 6, HorizontalAlignment.Center);
    }

    private static void DrawEmpty(ICanvas canvas, RectF rect)
    {
        var size = Math.Min(rect.Width, rect.Height);
        var r = size / 2f - 4f;
        canvas.StrokeColor = ParseColor("#C8C8C8");
        canvas.StrokeSize = 2;
        canvas.DrawCircle(rect.Center.X, rect.Center.Y, r);
        canvas.FontColor = ParseColor("#919191");
        canvas.FontSize = 13;
        canvas.DrawString("Sem despesas", rect.Center.X, rect.Center.Y, HorizontalAlignment.Center);
    }

    private static Color ParseColor(string hex)
    {
        try { return Color.FromArgb(hex); }
        catch { return Color.FromArgb("#9E9E9E"); }
    }
}
