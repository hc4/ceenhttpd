﻿using System;
using System.Threading.Tasks;

namespace Ceen.Security.Login
{
	/// <summary>
	/// Class that performs a logout action, and can be useds a redirect target or an API target
	/// </summary>
	public class LogoutHandler : LoginSettingsModule, IHttpModule
	{
		/// <summary>
		/// Gets or sets the url to redirect responses to after logout.
		/// Set this to an empty string if the status code is not a redirect.
		/// </summary>
		public string RedirectUrl { get; set; } = "/";
		/// <summary>
		/// Gets or sets the status code reported after logout.
		/// </summary>
		public int ResultStatusCode { get; set; } = 302;
		/// <summary>
		/// Gets or sets the status message reported after logout.
		/// </summary>
		public string ResultStatusMessage { get; set; } = "Found";

		/// <summary>
		/// Handles the request
		/// </summary>
		/// <returns>The awaitable task.</returns>
		/// <param name="context">The requests context.</param>
		public async Task<bool> HandleAsync(IHttpContext context)
		{
			var xsrf = context.Request.Headers[XSRFHeaderName] ?? context.Request.Cookies[XSRFCookieName];
			var cookie = context.Request.Cookies[AuthSessionCookieName];
			var longterm = context.Request.Cookies[AuthCookieName];
			var droppedxsrf = false;

			if (!string.IsNullOrWhiteSpace(xsrf))
			{
				var session = await ShortTermStorage.GetSessionFromXSRFAsync(xsrf);
				if (session != null)
				{
					// Kill the existing record
					await ShortTermStorage.DropSessionAsync(session);
					droppedxsrf = true;

					// Register a new one on the same XSRF token,
					// but without any user attached
					await ShortTermStorage.AddSessionAsync(new SessionRecord() {
						UserID = null,
						Cookie = null,
						XSRFToken = session.XSRFToken,
						Expires = DateTime.Now.AddSeconds(ShortTermExpirationSeconds)
					});
				}

				//context.Response.AddCookie(XSRFCookieName, "", path: CookiePath, expires: new DateTime(1970, 1, 1), maxage: 0);
			}

			if (!string.IsNullOrWhiteSpace(cookie))
			{
				var session = await ShortTermStorage.GetSessionFromCookieAsync(cookie);
				if (session != null)
					await ShortTermStorage.DropSessionAsync(session);

				if (session != null || droppedxsrf)
					context.Response.AddCookie(AuthSessionCookieName, "", path: CookiePath, expires: new DateTime(1970, 1, 1), maxage: 0);
			}

			if (!string.IsNullOrWhiteSpace(longterm))
			{
				if (LongTermStorage != null)
				{
					var pbkdf2 = new LongTermCookie(longterm);
					if (pbkdf2.IsValid)
					{
						var lts = await LongTermStorage.GetLongTermLoginAsync(pbkdf2.Series);
						if (lts != null)
							await LongTermStorage.DropLongTermLoginAsync(lts);
					}
				}

				context.Response.AddCookie(AuthCookieName, "", path: CookiePath, expires: new DateTime(1970, 1, 1), maxage: 0);
			}

			context.Response.StatusCode = (HttpStatusCode)ResultStatusCode;
			context.Response.StatusMessage = ResultStatusMessage;
			if (!string.IsNullOrWhiteSpace(RedirectUrl))
				context.Response.Headers["Location"] = RedirectUrl;

			return true;
		}
	}
}
