using System.ComponentModel;
using System.Reflection;

namespace FinanceApp.Domain.Common;

public static class EnumExtensions
{
    /// <summary>
    /// Retorna o texto do atributo <see cref="DescriptionAttribute"/> do membro do enum
    /// (usado para exibir rótulos em português). Se não houver, devolve o nome do membro.
    /// </summary>
    public static string GetDescription(this Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
        return attribute?.Description ?? value.ToString();
    }
}
