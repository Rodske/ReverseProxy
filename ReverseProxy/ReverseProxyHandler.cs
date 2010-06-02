using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Web;
using System.Net;
using System.Configuration;
using System.IO;


namespace ReverseProxy
{
    public class ReverseProxyHandler : IHttpAsyncHandler
    {
        static bool _enabled;
        static Hashtable _translations;
        // static Hashtable destinations;

        static ReverseProxyHandler()
        {
            ProxyConfigurationSection __proxyConfiguration = ConfigurationManager.GetSection("proxy") as ProxyConfigurationSection;

            if (__proxyConfiguration != null)
            {
                _translations = new Hashtable();

                foreach (ProxyTranslation __translation in __proxyConfiguration.translations)
                {
                    _translations[__translation.request] = __translation;
                }
                _enabled = true;
            }
            else {
                _enabled = false;
            }
        }

        public IAsyncResult BeginProcessRequest(HttpContext context, AsyncCallback cb, object extraData)
        {
            if (_enabled)
            {
                string __path = context.Request.AppRelativeCurrentExecutionFilePath; // Needs to be relative so it matches the values on the web.config
                if (__path.StartsWith("~")) __path= __path.Substring(1); // stripping the ~
                ProxyTranslation __translation = ((ProxyTranslation)_translations[__path]);
                Uri _uri = new Uri(string.Format("{0}{1}?{2}", __translation.destination, context.Request.PathInfo, context.Request.QueryString));
                HttpWebRequest _request = (HttpWebRequest)HttpWebRequest.Create(_uri);
                _request.Method = context.Request.HttpMethod;

                foreach (string _header in GetFilteredHeaders(__translation.headers,context.Request.Headers.AllKeys))
                    _request.Headers.Add(_header, context.Request.Headers[_header]);

                return new WebRequestResult(_request, __translation, cb, context, extraData);
            }
            else
            {
                return null;
            }
        }

        public void EndProcessRequest(IAsyncResult result)
        {
            WebRequestResult _state = (WebRequestResult)result;
            if (_state.response != null)
            {
                _AddHeadersToResponse(_state.response, _state.context, _state.translation);

                CopyStream(_state.response.GetResponseStream(), (_state.context.Response.OutputStream));
            }
            else if (_state.exception != null)
            {
                HttpWebResponse __response = (HttpWebResponse)_state.exception.Response;
                if (__response == null)
                {
                    _state.context.Response.StatusCode = 404;
                    _state.context.Response.StatusDescription = _state.exception.Message;
                    _state.context.Response.End();
                }
                else {
                    _state.context.Response.StatusCode = (int)__response.StatusCode;
                    _state.context.Response.StatusDescription = __response.StatusDescription;
                    _AddHeadersToResponse(__response, _state.context, _state.translation);
                    _state.context.Response.End();
                }
            }
        }

        private void _AddHeadersToResponse(WebResponse pResponse, HttpContext pContext, ProxyTranslation pTranslation)
        {
            foreach (string _header in GetFilteredHeaders(pTranslation.headers, pResponse.Headers.AllKeys))
                pContext.Response.AddHeader(_header, pResponse.Headers[_header]);
        }

        public bool IsReusable
        {
            get { return true; }
        }

        public void ProcessRequest(HttpContext context)
        {
            throw new NotImplementedException();
        }

        internal IEnumerable<string> GetFilteredHeaders ( string[] pAcceptedHeaders, IEnumerable<string> pHeaderCollection )
        {
            foreach( string _header in pHeaderCollection)
                if (Array.Exists(pAcceptedHeaders, _header.Equals))
                    yield return _header;
        }

        public static long CopyStream(Stream source, Stream target)
        {
            const int bufSize = 0x1000;
            byte[] buf = new byte[bufSize];
            long totalBytes = 0;
            int bytesRead = 0;
            while ((bytesRead = source.Read(buf, 0, bufSize)) > 0)
            {
                target.Write(buf, 0, bytesRead);
                totalBytes += bytesRead;
            }
            return totalBytes;
        }
    }
}
