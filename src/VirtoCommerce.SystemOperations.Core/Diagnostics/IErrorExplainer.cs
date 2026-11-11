namespace VirtoCommerce.SystemOperations.Core.Diagnostics;

public interface IErrorExplainer
{
    // Explains an error from its exception type, message and stack trace: of the journal, or pasted by a person.
    ErrorExplanation Explain(string exceptionType, string message, string stackTrace);
}
