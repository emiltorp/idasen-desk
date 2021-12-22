﻿using System.Threading.Tasks ;
using Microsoft.AspNetCore.Mvc ;
using Microsoft.AspNetCore.Mvc.Filters ;

namespace Idasen.RESTAPI.Filters
{
    public class ValidationFilter : IAsyncActionFilter
    {
        public void OnActionExecuted ( ActionExecutedContext context )
        {
        }

        public async Task OnActionExecutionAsync ( ActionExecutingContext  context ,
                                             ActionExecutionDelegate next )
        {
            if (!context.ModelState.IsValid)
                context.Result = new BadRequestObjectResult(context.ModelState);

            await next ( ) ;
        }
    }
}