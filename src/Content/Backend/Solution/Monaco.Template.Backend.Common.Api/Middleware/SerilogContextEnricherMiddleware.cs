using Microsoft.AspNetCore.Http;
using Serilog.Context;
using System.Security.Claims;

namespace Monaco.Template.Backend.Common.Api.Middleware;

public class SerilogContextEnricherMiddleware(RequestDelegate next)
{
	private const string UserIdType = "sub";
	private const string UserNameType = "preferred_username";

	public Task Invoke(HttpContext context)
	{
		var user = context.User;
		if (user.HasClaim(x => x.Type == UserIdType))
			LogContext.PushProperty("userId", context.User.FindFirstValue(UserIdType));

		if (user.HasClaim(x => x.Type == UserNameType))
			LogContext.PushProperty("userName", context.User.FindFirstValue(UserNameType));

		return next(context);
	}
}