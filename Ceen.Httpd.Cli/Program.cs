﻿using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ceen.Httpd;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Ceen.Httpd.Logging;
using Ceen.Httpd.Handler;
using Newtonsoft.Json;
using Ceen.Mvc;

namespace Ceen.Httpd.Cli
{
	class MainClass
	{
		private class TimeOfDayHandler : IHttpModule
		{
			#region IHttpModule implementation
			public async Task<bool> HandleAsync(IHttpContext context)
			{
				context.Response.SetNonCacheable();
				await context.Response.WriteAllJsonAsync(JsonConvert.SerializeObject(new { time = DateTime.Now.TimeOfDay } ));
				return true;
			}
			#endregion
		}

		private const string API_V1_NAMESPACE = "/api/v1/";

		[Name("api")]
		public interface IAPI : IControllerPrefix { }

		[Name("v1")]
		public interface IApiV1 : IAPI { }

		[Name("entry")]
		public class ApiExampleController : Controller, IApiV1
		{

			[HttpGet]
			public IResult Index(IHttpContext context)
			{
				return Json(new { ID = "From non-arg" });
			}

			[HttpPost]
			public void Update(int id)
			{
			}

			[HttpGet]
			[HttpDelete]
			[Route("{id}/vss-{*blurp}")]
			[Route("{id}")]
			public IResult Index(IHttpContext context, [Parameter(Source = ParameterSource.Url)]int id, string blurp = "my-blurp")
			{
				return Json(new { ID = $"From with-arg, id={id}, blurp={blurp}" });
			}

			[Route("{id}")]
			public IResult Detail(IHttpContext context, int id)
			{
				return Json(new { ID = "From detail with-arg" });
			}

			public IResult Detail(IHttpContext context)
			{
				return Json(new { ID = "From detail without-arg" });
			}

		}

		[Name("wait")]
		public class WaitExample : Controller, IApiV1
		{
			public async Task<IResult> Index()
			{
				await Task.Delay(TimeSpan.FromSeconds(30));
				return Text(HttpServer.TotalActiveClients.ToString());
			}
		}

		[Name("home")]
		[Route("/")]
		public class HomeController : Controller
		{
			public IResult Index()
			{
				return Html("<html><body>Hello!</body></html>");
			}
		}

		public static int Main(string[] args)
		{
			// Create a certificate with OpenSSL:
			// > openssl req -x509 -sha256 -nodes -days 365 -newkey rsa:2048 -keyout privkey.key -out certificate.crt

			// Convert it to pcks12
			// > openssl pkcs12 -export -in certificate.crt -inkey privkey.key -out certificate.pfx

			X509Certificate cert = null;

			if (File.Exists("certificate.pfx"))
				cert = new X509Certificate2("certificate.pfx", "");

			if (args == null || args.Length == 0)
				args = new string[] { Path.GetFullPath(".") };

			if (args.Length != 1)
			{
				Console.WriteLine("Usage: Ceenhttpd-cli [path-to-webroot]");
				return 1;
			}

			if (!Directory.Exists(args[0]))
			{
				Console.WriteLine("Directory not found: {0}", args[0]);
				return 1;
			}

			var server = new HttpServer(
				new ServerConfig() 
					{ SSLCertificate = cert }
					
					.AddRoute("/data", new TimeOfDayHandler())

					/*.AddRoute(null, new Mvc.ControllerRouter(
						new ControllerRouterConfig() {
							RoutePrefix = API_V1_NAMESPACE
						},
						typeof(MainClass).Assembly))
					*/
					.AddRoute(
						typeof(MainClass)
						.Assembly
						.ToRoute(
							new ControllerRouterConfig(typeof(HomeController))
							{ Debug = true }
					))

					.AddRoute(new FileHandler(args[0]))

					.AddLogger(new CLFLogger(Console.OpenStandardOutput()))
					.AddLogger(new SyslogLogger())
					.AddLogger((ctx, ex, start, duration) =>
					{
						if (ex != null)
							Console.WriteLine(ex);
						return Task.FromResult(true);
					})
			);

			var tcs = new System.Threading.CancellationTokenSource();
			var task = 
				Task.WhenAll(
					server.ListenAsync(new IPEndPoint(IPAddress.Loopback, 8900), tcs.Token),
					cert == null 
						? Task.FromResult(true) // No certificate, do not host SSL
						: server.ListenSSLAsync(new IPEndPoint(IPAddress.Loopback, 8901), stoptoken: tcs.Token) //Host using the given cert
				);

			Console.WriteLine("Server is running...");

			task.Wait();
			tcs.Cancel();

			Console.WriteLine("Server has stopped...");
			task.Wait();

			return 0;
		}
	}
}
