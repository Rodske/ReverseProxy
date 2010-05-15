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
        static string[] _acceptedHeaders;
        static string _destination;
        static bool _enabled;
        // static Hashtable destinations;

        static ReverseProxyHandler()
        {
            try
            {
                _destination = ConfigurationManager.AppSettings["PROXY_DESTINATION_URL"].ToString();
                _acceptedHeaders = ConfigurationManager.AppSettings["PROXY_ACCEPTED_HEADERS"].ToString().Split(',');
                _enabled = true;
            }
            catch
            {
                _enabled = false;
            }
        }

        public IAsyncResult BeginProcessRequest(HttpContext context, AsyncCallback cb, object extraData)
        {
            if (_enabled)
            {
                Uri _uri = new Uri(string.Format("{0}?{1}", _destination, context.Request.QueryString));

                HttpWebRequest _request = (HttpWebRequest)HttpWebRequest.Create(_uri);
                _request.Method = context.Request.HttpMethod;

                //TODO: HTTPWebRequest is not supporting  adding headers, find a work around
                //foreach (string _header in GetFilteredHeaders(context.Request.Headers.AllKeys))
                //    _request.Headers.Add(_header, context.Request.Headers[_header]);

                return new WebRequestResult(_request, cb, context, extraData);
            }
            else
            {
                return null;
            }
        }

        public void EndProcessRequest(IAsyncResult result)
        {
            WebRequestResult _state = (WebRequestResult)result;
            WebResponse _response = _state.response;
            HttpContext _context = _state.context;

            foreach ( string _header in GetFilteredHeaders(_response.Headers.AllKeys))
                _context.Response.AddHeader(_header, _response.Headers[_header]);

            CopyStream(_response.GetResponseStream(),(_context.Response.OutputStream));
        }

        public bool IsReusable
        {
            get { return true; }
        }

        public void ProcessRequest(HttpContext context)
        {
            throw new NotImplementedException();
        }

        internal IEnumerable<string> GetFilteredHeaders ( IEnumerable<string> pHeaderCollection )
        {
            foreach( string _header in pHeaderCollection)
                if (Array.Exists(_acceptedHeaders, _header.Equals))
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

    public class WebRequestResult : IAsyncResult
    {
        public WebResponse response { get; private set; }
        public HttpContext context { get; private set; }
        public bool CompletedSynchronously { get { return false; } }
        public bool IsCompleted { get; private set; }
        public object AsyncState { get; private set; }
        HttpWebRequest _request;
        AsyncCallback _callback;

        public System.Threading.WaitHandle AsyncWaitHandle
        {
            get { throw new NotImplementedException(); }
        }

        public WebRequestResult(HttpWebRequest pRequest, AsyncCallback pCallback, HttpContext pContext, object pState)
        {
            context = pContext;
            _request = pRequest;
            _callback = pCallback;
            IsCompleted = false;
            AsyncState = pState;

            if (_request.Method.Equals("POST", StringComparison.InvariantCultureIgnoreCase))
                _request.BeginGetRequestStream(new AsyncCallback(AsyncReturnStream), this);
            else
                _request.BeginGetResponse(new AsyncCallback(AsyncReturn), this);
        }
        private void AsyncReturnStream (IAsyncResult result)
        {
            Stream _stream = _request.EndGetRequestStream(result);
            ReverseProxyHandler.CopyStream(context.Request.InputStream,_stream);
            _stream.Close();
            _request.BeginGetResponse(new AsyncCallback(AsyncReturn), this);
        }
        private void AsyncReturn(IAsyncResult result)
        {
            IsCompleted = true;
            response = _request.EndGetResponse(result);
            if (_callback != null)
                _callback(this);
        }
    }
}
