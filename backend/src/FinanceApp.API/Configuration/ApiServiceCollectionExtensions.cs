using System.Net;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FinanceApp.API.Errors;
using FinanceApp.API.Json;
using FinanceApp.API.RateLimiting;
using FinanceApp.API.Security;
using FinanceApp.Application.Common.Abstractions;
using FinanceApp.Application.Common.Exceptions;
using FinanceApp.Application.Common.Options;
using FinanceApp.Infrastructure.Persistence;
using FinanceApp.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

namespace FinanceApp.API.Configuration;

public static class ApiServiceCollectionExtensions
{
    public const string CorsPolicyName = "Frontend";

    public static IServiceCollection AddApi(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddSingleton<RefreshTokenCookie>();

        services
            .AddControllers(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
            .AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                o.JsonSerializerOptions.Converters.Add(new UtcDateTimeOffsetConverter());
            })
            .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = InvalidModelStateResponse.Create);

        services.ConfigureHttpJsonOptions(o =>
        {
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            o.SerializerOptions.Converters.Add(new UtcDateTimeOffsetConverter());
        });

        services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx => ProblemDetailsBuilder.ApplyDefaults(ctx.HttpContext, ctx.ProblemDetails));
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddJwtAuthentication();
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        services.AddAuthRateLimiting();
        services.AddFrontendCors();
        services.AddForwardedHeadersFromConfiguration();

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");
        services.AddSwagger();

        return services;
    }

    private static void AddJwtAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((o, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = JwtSigningKey.Create(jwt.Secret),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                };
                o.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        if (context.Response.HasStarted)
                        {
                            return;
                        }

                        context.Response.Headers.WWWAuthenticate = context.AuthenticateFailure is null
                            ? "Bearer"
                            : "Bearer error=\"invalid_token\"";
                        var problem = ProblemDetailsBuilder.Create(
                            context.HttpContext,
                            StatusCodes.Status401Unauthorized,
                            ErrorCodes.Unauthorized,
                            "Não autenticado.",
                            "Token de acesso ausente, inválido ou expirado.");
                        await ProblemResponseWriter.WriteAsync(context.HttpContext, problem).ConfigureAwait(false);
                    },
                    OnForbidden = async context =>
                    {
                        var problem = ProblemDetailsBuilder.Create(
                            context.HttpContext, StatusCodes.Status403Forbidden, ErrorCodes.Forbidden, "Acesso negado.");
                        await ProblemResponseWriter.WriteAsync(context.HttpContext, problem).ConfigureAwait(false);
                    },
                };
            });
    }

    private static void AddAuthRateLimiting(this IServiceCollection services)
    {
        services.AddOptions<RateLimitingOptions>().BindConfiguration(RateLimitingOptions.SectionName);
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.OnRejected = async (context, _) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                var problem = ProblemDetailsBuilder.Create(
                    context.HttpContext,
                    StatusCodes.Status429TooManyRequests,
                    ErrorCodes.TooManyRequests,
                    "Muitas requisições. Tente novamente mais tarde.");
                await ProblemResponseWriter.WriteAsync(context.HttpContext, problem).ConfigureAwait(false);
            };

            o.AddPolicy(RateLimitPolicies.Auth, http => Partition(http, RateLimitPolicies.Auth, opts => opts.Auth));
            o.AddPolicy(RateLimitPolicies.Refresh, http => Partition(http, RateLimitPolicies.Refresh, opts => opts.Refresh));
        });

        static RateLimitPartition<string> Partition(HttpContext http, string policy, Func<RateLimitingOptions, FixedWindowLimit> select)
        {
            var options = http.RequestServices.GetRequiredService<IOptionsMonitor<RateLimitingOptions>>().CurrentValue;
            if (!options.Enabled)
            {
                return RateLimitPartition.GetNoLimiter("disabled");
            }

            var limit = select(options);
            var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter($"{policy}:{ip}", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, limit.PermitLimit),
                Window = TimeSpan.FromSeconds(Math.Max(1, limit.WindowSeconds)),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
        }
    }

    private static void AddFrontendCors(this IServiceCollection services)
    {
        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IConfiguration>((o, configuration) =>
        {
            var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.TrimEnd('/'))
                .ToArray() ?? [];

            o.AddPolicy(CorsPolicyName, policy =>
            {
                if (origins.Length > 0)
                {
                    policy.WithOrigins(origins).AllowCredentials().AllowAnyHeader().AllowAnyMethod();
                }
            });
        });
    }

    /// <summary>
    /// Aceita X-Forwarded-For/Proto somente de proxies confiáveis (por padrão loopback e as redes
    /// listadas em <c>ForwardedHeaders:KnownNetworks</c>, ex.: a rede Docker do Nginx).
    /// </summary>
    private static void AddForwardedHeadersFromConfiguration(this IServiceCollection services)
    {
        services.AddOptions<ForwardedHeadersOptions>().Configure<IConfiguration>((o, configuration) =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.ForwardLimit = 1;
            foreach (var cidr in configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
            {
                var parts = cidr.Split('/', 2, StringSplitOptions.TrimEntries);
                if (parts.Length == 2 && IPAddress.TryParse(parts[0], out var prefix) && int.TryParse(parts[1], out var length))
                {
                    o.KnownNetworks.Add(new IPNetwork(prefix, length));
                }
                else
                {
                    throw new InvalidOperationException($"ForwardedHeaders:KnownNetworks contém um CIDR inválido: '{cidr}'.");
                }
            }
        });
    }

    private static void AddSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new OpenApiInfo { Title = "GroupFinance API", Version = "v1" });
            o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Access token JWT (sem o prefixo 'Bearer ').",
            });
            o.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                    Array.Empty<string>()
                },
            });
        });
    }
}
