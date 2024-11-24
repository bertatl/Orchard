using System;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;

namespace Orchard.WarmupStarter {
    public static class WarmupUtility {
        public static readonly string WarmupFilesPath = "~/App_Data/Warmup/";
        /// <summary>
        /// return true to put request on hold (until call to Signal()) - return false to allow pipeline to execute immediately
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static bool DoBeginRequest(HttpContext context) {
            // use the url as it was requested by the client
            // the real url might be different if it has been translated (proxy, load balancing, ...)
            var url = ToUrlString(context.Request);
            var virtualFileCopy = WarmupUtility.EncodeUrl(url.TrimStart('/'));
            var env = (IWebHostEnvironment)context.RequestServices.GetService(typeof(IWebHostEnvironment));
            var localCopy = Path.Combine(env.WebRootPath, WarmupFilesPath.TrimStart('~', '/'), virtualFileCopy);

            if (File.Exists(localCopy)) {
                // result should not be cached, even on proxies
                context.Response.Headers.Append("Cache-Control", "no-store, no-cache, must-revalidate, max-age=0");
                context.Response.Headers.Append("Pragma", "no-cache");
                context.Response.Headers.Append("Expires", "-1");

                context.Response.SendFileAsync(localCopy).Wait();
                return true;
            }

            // there is no local copy and the file exists
            // serve the static file
            if (File.Exists(context.Request.Path.Value)) {
                return true;
            }

            return false;
        }

        public static string ToUrlString(HttpRequest request) {
            return $"{request.Scheme}://{request.Host}{request.Path}{request.QueryString}";
        }

        public static string EncodeUrl(string url) {
            if (string.IsNullOrWhiteSpace(url)) {
                throw new ArgumentException("url can't be empty");
            }

            var sb = new StringBuilder();
            foreach (var c in url.ToLowerInvariant()) {
                // only accept alphanumeric chars
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) {
                    sb.Append(c);
                }
                // otherwise encode them in UTF8
                else {
                    sb.Append("_");
                    foreach (var b in Encoding.UTF8.GetBytes(new[] { c })) {
                        sb.Append(b.ToString("X"));
                    }
                }
            }

            return sb.ToString();
        }
    }
}