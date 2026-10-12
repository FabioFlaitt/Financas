namespace FinanceApp.Mobile.Views;

/// <summary>
/// Telas das demais abas. Nesta Fase 1 apresentam um "estado inicial" informativo descrevendo o
/// que será implementado nas próximas fases. Construídas em código para manter o projeto enxuto;
/// serão substituídas por telas completas com seus próprios ViewModels nas fases seguintes.
/// </summary>
public abstract class FeaturePlaceholderPage : ContentPage
{
    protected FeaturePlaceholderPage(string title, string emoji, string heading, string description, string phase)
    {
        Title = title;
        BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark ? Color.FromArgb("#101317") : Color.FromArgb("#F2F4F7");

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 28,
                Spacing = 14,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    new Label { Text = emoji, FontSize = 48, HorizontalOptions = LayoutOptions.Center },
                    new Label
                    {
                        Text = heading,
                        FontSize = 22,
                        FontAttributes = FontAttributes.Bold,
                        HorizontalTextAlignment = TextAlignment.Center,
                        TextColor = Color.FromArgb("#0D47A1")
                    },
                    new Label
                    {
                        Text = description,
                        FontSize = 15,
                        HorizontalTextAlignment = TextAlignment.Center,
                        TextColor = Color.FromArgb("#6E6E6E")
                    },
                    new Border
                    {
                        BackgroundColor = Color.FromArgb("#E3F2FD"),
                        Stroke = Color.FromArgb("#90CAF9"),
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                        Padding = new Thickness(12, 6),
                        HorizontalOptions = LayoutOptions.Center,
                        Content = new Label
                        {
                            Text = phase,
                            FontSize = 12,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Color.FromArgb("#0D47A1")
                        }
                    }
                }
            }
        };
    }
}
