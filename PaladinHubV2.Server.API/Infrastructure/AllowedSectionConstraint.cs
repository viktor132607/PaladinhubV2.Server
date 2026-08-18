using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace PaladinHubV2.Server.API.Infrastructure.Routing
{
	/// <summary>
	/// Разрешава само секции: holy | protection | retribution (case-insensitive).
	/// Пр.: {section:palsec}/Talents  ще приеме /Holy/Talents, /holy/talents, /HoLy/Talents ...
	/// </summary>
	public sealed class AllowedSectionConstraint : IRouteConstraint
	{
		public bool Match(HttpContext? httpContext,
						  IRouter? route,
						  string routeKey,
						  RouteValueDictionary values,
						  RouteDirection routeDirection)
		{
			if (values.TryGetValue(routeKey, out var raw) && raw is not null)
			{
				var s = raw.ToString()?.Trim().ToLowerInvariant();
				return s == "holy" || s == "protection" || s == "retribution";
			}
			return false;
		}
	}
}
