using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PremierLeaguePredictions.API.Filters;

public class ValidationFilter<T> : IAsyncActionFilter where T : class
{
    private readonly IValidator<T>? _validator;

    public ValidationFilter(IServiceProvider serviceProvider)
    {
        _validator = serviceProvider.GetService<IValidator<T>>();
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (_validator == null)
        {
            await next();
            return;
        }

        var model = context.ActionArguments.Values.OfType<T>().FirstOrDefault();
        if (model == null)
        {
            await next();
            return;
        }

        var validationResult = await _validator.ValidateAsync(model);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray()
                );

            context.Result = new BadRequestObjectResult(new
            {
                message = "Validation failed",
                errors = errors
            });
            return;
        }

        await next();
    }
}
