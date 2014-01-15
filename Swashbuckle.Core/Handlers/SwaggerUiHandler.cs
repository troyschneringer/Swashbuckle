﻿using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Swashbuckle.Core.Models;

namespace Swashbuckle.Core.Handlers
{
    public class SwaggerUiHandler : HttpMessageHandler
    {
        private readonly SwaggerSpecConfig _swaggerSpecConfig;
        private readonly SwaggerUiConfig _swaggerUiConfig;

        public SwaggerUiHandler()
        {
            _swaggerSpecConfig = SwaggerSpecConfig.Instance;
            _swaggerUiConfig = SwaggerUiConfig.Instance;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.GetRouteData().Values["path"];

            var responseMessage = (path == null) ? RedirectResponse(request) : UiResourceResponse(path.ToString());

            return Task.Factory.StartNew(() => responseMessage);
        }

        private HttpResponseMessage RedirectResponse(HttpRequestMessage request)
        {
            var basePath = _swaggerSpecConfig.BasePathResolver(request);

            var response = request.CreateResponse(HttpStatusCode.Moved);
            response.Headers.Location = new Uri(String.Format("{0}/swagger/ui/index.html", basePath.Trim('/')));

            return response;
        }

        private HttpResponseMessage UiResourceResponse(string path)
        {
            var resourceStream = path.StartsWith("ui/ext/")
                ? GetCustomResourceStream(path.Substring(7))
                : GetType().Assembly.GetManifestResourceStream(path.Substring(3));

            HttpContent content = new StreamContent(resourceStream);
            if (path == "ui/index.html")
                content = CustomizeIndexContent(content);

            content.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeFor(path));
            return new HttpResponseMessage { Content = content };
        }

        private Stream GetCustomResourceStream(string resourceName)
        {
            var customResourceDescriptor = _swaggerUiConfig.CustomScripts
                .Union(_swaggerUiConfig.CustomStylesheets)
                .Single(cs => cs.Name == resourceName);

            var stream = customResourceDescriptor.Assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new FileNotFoundException("Ensure the Build Action is set to \"Embedded Resource\"", resourceName);

            return stream;
        }

        private HttpContent CustomizeIndexContent(HttpContent content)
        {
            var originalText = content.ReadAsStringAsync().Result;

            var listOfSubmitMethods = String.Join(",", _swaggerUiConfig.SupportedSubmitMethods
                .Select(sm => String.Format("'{0}'", sm)));

            var scriptIncludes = String.Join("\r\n", _swaggerUiConfig.CustomScripts
                .Select(cs => String.Format("$.getScript('ext/{0}');", cs.Name)));

            var stylesheetIncludes = String.Join("\r\n", _swaggerUiConfig.CustomStylesheets
                .Select(cs => String.Format("<link href='ext/{0}' rel='stylesheet' type='text/css'/>", cs.Name)));

            var customizedText = originalText
                .Replace("%(DiscoveryUrl)", "window.location.href.replace(/ui\\/index\\.html.*/, 'api-docs')")
                .Replace("%(ApiKeyName)", String.Format("\"{0}\"", _swaggerUiConfig.ApiKeyName))
                .Replace("%(ApiKey)", String.Format("\"{0}\"", _swaggerUiConfig.ApiKey))
                .Replace("%(SupportHeaderParams)", _swaggerUiConfig.SupportHeaderParams.ToString().ToLower())
                .Replace("%(SupportedSubmitMethods)", String.Format("[{0}]", listOfSubmitMethods))
                .Replace("%(DocExpansion)", String.Format("\"{0}\"", _swaggerUiConfig.DocExpansion.ToString().ToLower()))
                .Replace("%(CustomScripts)", scriptIncludes)
                .Replace("%(CustomStylesheets)", stylesheetIncludes);

            return new StringContent(customizedText);
        }

        private static string MediaTypeFor(string path)
        {
            var extension = path.Split('.').Last();

            switch (extension)
            {
                case "css":
                    return "text/css";
                case "js":
                    return "text/javascript";
                default:
                    return "text/html";
            }
        }
    }
}