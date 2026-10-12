namespace FinanceApp.Application.Common.Exceptions;

/// <summary>Lançada quando um recurso solicitado não existe (ou não pertence ao usuário atual).</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }

    public static NotFoundException For(string entity, object id) =>
        new($"{entity} '{id}' não encontrado(a).");
}

/// <summary>Lançada quando a entrada falha nas regras de validação da aplicação.</summary>
public class AppValidationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public AppValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("Um ou mais erros de validação ocorreram.")
    {
        Errors = errors;
    }

    public AppValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = new[] { message } }) { }
}

/// <summary>Lançada quando uma regra de negócio é violada (ex.: saldo, conciliação).</summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message) { }
}
