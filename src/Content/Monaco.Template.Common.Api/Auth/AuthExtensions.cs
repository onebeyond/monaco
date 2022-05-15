using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Monaco.Template.Common.Api.Auth;

public static class AuthExtensions
{
    public const string ScopeClaimType = "scope";

    public static IServiceCollection AddAuthorizationWithPolicies(this IServiceCollection services, List<string> scopes)
    {
        return services.AddAuthorization(cfg =>
                                         {	 //DefaultPolicy will require at least authenticated user by default
                                             cfg.DefaultPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
                                                                 .RequireAuthenticatedUser().Build();
                                             //Register all listed scopes as policies requiring the existance of such scope in User claims
                                             scopes.ForEach(s => cfg.AddPolicy(s, p => p.RequireScope(s)));
                                         });
    }

    public static AuthenticationBuilder AddJwtBearerAuthentication(this IServiceCollection services, string authority, string audience, bool requireHttpsMetadata)
    {
        return services.AddTransient<IClaimsTransformation, ScopeClaimsTransformation>()	//Add transformer to map scopes correctly in ClaimsPrincipal/Identity
                       .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)		//Set Bearer scheme
                       .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme,			//Configure validation settings for JWT bearer
                                     options =>
                                     {
                                         options.Authority = authority;
                                         options.Audience = audience;
                                         options.RequireHttpsMetadata = requireHttpsMetadata;
                                         options.TokenValidationParameters.NameClaimType = "name";
                                         options.TokenValidationParameters.RoleClaimType = "roles";

                                         options.SecurityTokenValidators.Clear();
                                         options.SecurityTokenValidators.Add(new JwtSecurityTokenHandler {MapInboundClaims = false});

                                         options.TokenValidationParameters.ValidTypes = new[] {"JWT"};
                                     });
    }

    /// <summary>
    /// Requires claims of type "scope" with matching values
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="allowedValues"></param>
    /// <returns></returns>
    public static AuthorizationPolicyBuilder RequireScope(this AuthorizationPolicyBuilder builder, params string[] allowedValues)
    {
        return builder.RequireClaim(ScopeClaimType, (IEnumerable<string>)allowedValues);
    }
}