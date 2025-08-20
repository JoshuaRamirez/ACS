using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using ACS.WebApi.Security.Validation;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace ACS.WebApi.Security.Filters;

/// <summary>
/// Action filter for automatic input validation and sanitization
/// </summary>
public class InputValidationActionFilter : IAsyncActionFilter
{
    private readonly IInputValidator _validator;
    private readonly ILogger<InputValidationActionFilter> _logger;

    public InputValidationActionFilter(IInputValidator validator, ILogger<InputValidationActionFilter> logger)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        // Validate all action arguments
        foreach (var argument in context.ActionArguments)
        {
            if (argument.Value == null) continue;

            var argumentType = argument.Value.GetType();

            // Skip primitive types and system types
            if (argumentType.IsPrimitive || argumentType.Namespace?.StartsWith("System") == true)
            {
                // For strings, apply basic validation
                if (argument.Value is string stringValue)
                {
                    var validationContext = new ValidationContext(stringValue) { MemberName = argument.Key };
                    var result = _validator.ValidateAndSanitize(stringValue, validationContext);
                    
                    if (result != ValidationResult.Success)
                    {
                        context.ModelState.AddModelError(argument.Key, result.ErrorMessage ?? "Invalid input");
                    }
                }
                continue;
            }

            // Validate complex objects
            try
            {
                ValidateObject(argument.Value, argument.Key, context.ModelState);
            }
            catch (ValidationException ex)
            {
                _logger.LogWarning(ex, "Validation failed for argument {ArgumentName}", argument.Key);
                context.ModelState.AddModelError(argument.Key, ex.Message);
            }
        }

        // If validation failed, return bad request
        if (!context.ModelState.IsValid)
        {
            var errors = context.ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
                );

            _logger.LogWarning("Request validation failed with {ErrorCount} errors", errors.Count);

            context.Result = new BadRequestObjectResult(new
            {
                Message = "Validation failed",
                Errors = errors
            });
            return;
        }

        await next();
    }

    private void ValidateObject(object obj, string path, ModelStateDictionary modelState)
    {
        if (obj == null) return;

        var type = obj.GetType();

        // Handle collections
        if (obj is System.Collections.IEnumerable enumerable && !(obj is string))
        {
            int index = 0;
            foreach (var item in enumerable)
            {
                if (item != null)
                {
                    ValidateObject(item, $"{path}[{index}]", modelState);
                }
                index++;
            }
            return;
        }

        // Validate the object itself
        var validationResults = _validator.GetValidationErrors(obj);
        foreach (var result in validationResults)
        {
            var memberNames = result.MemberNames.Any() 
                ? string.Join(".", result.MemberNames) 
                : path;
            modelState.AddModelError($"{path}.{memberNames}", result.ErrorMessage ?? "Validation failed");
        }

        // Process string properties for sanitization
        var properties = type.GetProperties()
            .Where(p => p.CanRead && p.CanWrite);

        foreach (var property in properties)
        {
            var value = property.GetValue(obj);
            if (value == null) continue;

            var propertyPath = $"{path}.{property.Name}";

            if (property.PropertyType == typeof(string))
            {
                var stringValue = value as string;
                if (!string.IsNullOrEmpty(stringValue))
                {
                    // Check for security validation attributes
                    var attributes = property.GetCustomAttributes<ValidationAttribute>();
                    foreach (var attribute in attributes)
                    {
                        var validationContext = new ValidationContext(obj) { MemberName = property.Name };
                        var result = attribute.GetValidationResult(stringValue, validationContext);
                        
                        if (result != ValidationResult.Success)
                        {
                            modelState.AddModelError(propertyPath, result?.ErrorMessage ?? "Validation failed");
                        }
                    }

                    // Apply general validation
                    var context = new ValidationContext(stringValue) { MemberName = property.Name };
                    var validationResult = _validator.ValidateAndSanitize(stringValue, context);
                    
                    if (validationResult != ValidationResult.Success)
                    {
                        modelState.AddModelError(propertyPath, validationResult.ErrorMessage ?? "Invalid input");
                    }
                }
            }
            else if (!property.PropertyType.IsPrimitive && 
                     !property.PropertyType.Namespace?.StartsWith("System") == true)
            {
                // Recursively validate nested objects
                ValidateObject(value, propertyPath, modelState);
            }
        }
    }
}

/// <summary>
/// Attribute to skip input validation for specific actions
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class SkipInputValidationAttribute : Attribute
{
}

/// <summary>
/// Global exception filter for validation exceptions
/// </summary>
public class ValidationExceptionFilter : IExceptionFilter
{
    private readonly ILogger<ValidationExceptionFilter> _logger;

    public ValidationExceptionFilter(ILogger<ValidationExceptionFilter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is ValidationException validationException)
        {
            _logger.LogWarning(validationException, "Validation exception occurred");

            var response = new
            {
                Message = "Validation failed",
                Error = validationException.Message,
                ValidationResult = validationException.ValidationResult
            };

            context.Result = new BadRequestObjectResult(response);
            context.ExceptionHandled = true;
        }
    }
}

/// <summary>
/// Model binder for automatic input sanitization
/// </summary>
public class SanitizingModelBinder : IModelBinder
{
    private readonly IInputValidator _validator;
    private readonly IModelBinder _innerBinder;

    public SanitizingModelBinder(IInputValidator validator, IModelBinder innerBinder)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _innerBinder = innerBinder ?? throw new ArgumentNullException(nameof(innerBinder));
    }

    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        await _innerBinder.BindModelAsync(bindingContext);

        if (bindingContext.Result.IsModelSet && bindingContext.Result.Model != null)
        {
            var model = bindingContext.Result.Model;
            
            // Sanitize string values
            if (model is string stringValue)
            {
                var sanitized = _validator.SanitizeHtml(stringValue);
                bindingContext.Result = ModelBindingResult.Success(sanitized);
            }
            else
            {
                // For complex objects, use the validation service
                try
                {
                    var sanitized = _validator.ValidateAndSanitizeObject(model);
                    bindingContext.Result = ModelBindingResult.Success(sanitized);
                }
                catch (ValidationException ex)
                {
                    bindingContext.ModelState.AddModelError(bindingContext.ModelName, ex.Message);
                    bindingContext.Result = ModelBindingResult.Failed();
                }
            }
        }
    }
}

/// <summary>
/// Model binder provider for sanitizing model binder
/// </summary>
public class SanitizingModelBinderProvider : IModelBinderProvider
{
    private readonly IInputValidator _validator;

    public SanitizingModelBinderProvider(IInputValidator validator)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        // Apply to all non-simple types
        if (!context.Metadata.IsComplexType) return null;

        var innerBinder = new ComplexObjectModelBinder(
            context.Metadata.Properties,
            context.Services.GetRequiredService<ILoggerFactory>());

        return new SanitizingModelBinder(_validator, innerBinder);
    }
}