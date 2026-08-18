using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace PaladinHubV2.Server.Domain.Services.PageBuilder
{
	public sealed class BlockRenderer : IBlockRenderer
	{
		private readonly IRazorViewEngine _viewEngine;
		private readonly ITempDataProvider _tempDataProvider;
		private readonly IServiceProvider _serviceProvider;

		public BlockRenderer(
			IRazorViewEngine viewEngine,
			ITempDataProvider tempDataProvider,
			IServiceProvider serviceProvider)
		{
			_viewEngine = viewEngine;
			_tempDataProvider = tempDataProvider;
			_serviceProvider = serviceProvider;
		}

		public async Task<string> RenderAsync(string jsonLayout)
		{
			using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(jsonLayout) ? "[]" : jsonLayout);
			var root = doc.RootElement;
			if (root.ValueKind != JsonValueKind.Array)
				throw new InvalidOperationException("Layout must be a JSON array of blocks.");

			var sb = new StringBuilder();

			foreach (var block in root.EnumerateArray())
			{
				var rawType = block.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
				var type = (rawType ?? string.Empty).Trim();
				if (string.IsNullOrWhiteSpace(type))
				{
					sb.Append("<div class=\"alert alert-warning\">Block missing <code>type</code>.</div>");
					continue;
				}

				var (view, searched) = TryResolveView(type);

				if (view == null)
				{
					var searchedHtml = string.Join("<br/>", searched.Distinct().Select(System.Net.WebUtility.HtmlEncode));
					sb.Append($@"<div class=""alert alert-danger"">
						Block render failed: type '{System.Net.WebUtility.HtmlEncode(type)}' not found in any known partial.
						<br/><b>Searched locations:</b><br/>{searchedHtml}
					</div>");
					continue;
				}

				var html = await RenderFoundViewToStringAsync(view, model: block);
				sb.Append(html);
			}

			return sb.ToString();
		}

		private (IView? view, List<string> searched) TryResolveView(string type)
		{
			var searched = new List<string>();

			// Нормализираме към PascalCase, но ще пробваме и lower/оригинал.
			var pascal = ToPascal(type);                 // e.g. "Heading", "Table.Generic", "TalentTree"
			var lower = pascal.Length > 0
				? char.ToLowerInvariant(pascal[0]) + pascal[1..]   // "heading", "table.Generic"
				: string.Empty;
			var pascalNoDots = pascal.Replace('.', '_'); // "Table_Generic"
			var lowerNoDots = lower.Replace('.', '_');  // "table_Generic"

			// ---- 1) Кандидати по ИМЕ (FindView) – спазва ViewLocations и работи и за RCL
			var viewNameCandidates = new[]
			{
				// Pascal
				$"Blocks/_{pascal}",
				$"Blocks/{pascal}",
				$"Blocks/_Block.{pascal}",
				$"Blocks/_Block_{pascalNoDots}",

				// Lower (case-insensitive alias)
				$"Blocks/_{lower}",
				$"Blocks/{lower}",
				$"Blocks/_Block.{lower}",
				$"Blocks/_Block_{lowerNoDots}",
			};

			foreach (var name in viewNameCandidates)
			{
				var (view, list) = TryFindView(name);
				searched.AddRange(list);
				if (view != null) return (view, searched);
			}

			// ---- 2) Абсолютни пътища (GetView) – пробваме и "~/" и "/" варианти
			var absPathCandidates = new[]
			{
				// Pascal
				$"~/Views/Shared/Blocks/_{pascal}.cshtml",
				$"~/Views/Shared/Blocks/{pascal}.cshtml",
				$"~/Views/Shared/Blocks/_Block.{pascal}.cshtml",
				$"~/Views/Shared/Blocks/_Block_{pascalNoDots}.cshtml",

				$"/Views/Shared/Blocks/_{pascal}.cshtml",
				$"/Views/Shared/Blocks/{pascal}.cshtml",
				$"/Views/Shared/Blocks/_Block.{pascal}.cshtml",
				$"/Views/Shared/Blocks/_Block_{pascalNoDots}.cshtml",

				// Lower (case-insensitive alias)
				$"~/Views/Shared/Blocks/_{lower}.cshtml",
				$"~/Views/Shared/Blocks/{lower}.cshtml",
				$"~/Views/Shared/Blocks/_Block.{lower}.cshtml",
				$"~/Views/Shared/Blocks/_Block_{lowerNoDots}.cshtml",

				$"/Views/Shared/Blocks/_{lower}.cshtml",
				$"/Views/Shared/Blocks/{lower}.cshtml",
				$"/Views/Shared/Blocks/_Block.{lower}.cshtml",
				$"/Views/Shared/Blocks/_Block_{lowerNoDots}.cshtml",
			};

			foreach (var path in absPathCandidates)
			{
				var (view, list) = TryGetView(path);
				searched.AddRange(list);
				if (view != null) return (view, searched);
			}

			// ---- 3) Ако стигнем дотук – не открихме нищо
			return (null, searched);
		}

		private (IView? view, List<string> searched) TryFindView(string viewName)
		{
			var searched = new List<string>();
			var actionCtx = new ActionContext(
				new DefaultHttpContext { RequestServices = _serviceProvider },
				new Microsoft.AspNetCore.Routing.RouteData(),
				new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());

			var result = _viewEngine.FindView(actionCtx, viewName, isMainPage: false);
			if (result.Success) return (result.View, searched);
			if (result is ViewEngineResult ver && ver.SearchedLocations != null) searched.AddRange(ver.SearchedLocations);
			return (null, searched);
		}

		private (IView? view, List<string> searched) TryGetView(string absolutePath)
		{
			var searched = new List<string>();
			var result = _viewEngine.GetView(executingFilePath: null, viewPath: absolutePath, isMainPage: false);
			if (result.Success) return (result.View, searched);
			if (result is ViewEngineResult ver && ver.SearchedLocations != null) searched.AddRange(ver.SearchedLocations);

			// При GetView обикновено няма SearchedLocations – добавяме пътя за яснота
			if (!string.IsNullOrWhiteSpace(absolutePath)) searched.Add(absolutePath);
			return (null, searched);
		}

		private async Task<string> RenderFoundViewToStringAsync(IView view, object? model)
		{
			var actionContext = new ActionContext(
				new DefaultHttpContext { RequestServices = _serviceProvider },
				new Microsoft.AspNetCore.Routing.RouteData(),
				new Microsoft.AspNetCore.Mvc.Abstractions.ActionDescriptor());

			await using var sw = new StringWriter();
			var viewDictionary = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
			{
				Model = model
			};
			var tempData = new TempDataDictionary(actionContext.HttpContext, _tempDataProvider);

			var viewContext = new ViewContext(
				actionContext,
				view,
				viewDictionary,
				tempData,
				sw,
				new HtmlHelperOptions());

			await view.RenderAsync(viewContext);
			return sw.ToString();
		}

		private static string ToPascal(string type)
		{
			var parts = type.Split(new[] { '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
			return string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
		}
	}
}
