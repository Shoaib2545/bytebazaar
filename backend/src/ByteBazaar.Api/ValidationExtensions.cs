using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ByteBazaar.Api;

public static class ValidationExtensions
{
    public static ModelStateDictionary ToModelState(this ValidationResult validation, ModelStateDictionary modelState)
    {
        foreach (var error in validation.Errors)
            modelState.AddModelError(error.PropertyName, error.ErrorMessage);
        return modelState;
    }
}
