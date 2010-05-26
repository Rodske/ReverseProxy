using System.Web;
using System.Net;
using System.IO;
using System;

namespace ReverseProxy
{
    public class WebRequestResult : IAsyncResult
    {
        public WebResponse response { get; private set; }
        public WebException exception { get; private set; }
        public HttpContext context { get; private set; }
        public ProxyTranslation translation { get; private set; }
        public bool CompletedSynchronously { get { return false; } }
        public bool IsCompleted { get; private set; }
        public object AsyncState { get; private set; }
        HttpWebRequest _request;
        AsyncCallback _callback;

        public System.Threading.WaitHandle AsyncWaitHandle
        {
            get { throw new NotImplementedException(); }
        }

        public WebRequestResult(HttpWebRequest pRequest, ProxyTranslation pTranslation, AsyncCallback pCallback, HttpContext pContext, object pState)
        {
            context = pContext;
            _request = pRequest;
            _callback = pCallback;
            translation = pTranslation;
            IsCompleted = false;
            AsyncState = pState;

            if (_request.Method.Equals("POST", StringComparison.InvariantCultureIgnoreCase))
                _request.BeginGetRequestStream(new AsyncCallback(AsyncReturnStream), this);
            else
                _request.BeginGetResponse(new AsyncCallback(AsyncReturn), this);
        }
        private void AsyncReturnStream(IAsyncResult result)
        {
            Stream _stream = _request.EndGetRequestStream(result);
            ReverseProxyHandler.CopyStream(context.Request.InputStream, _stream);
            _stream.Close();
            _request.BeginGetResponse(new AsyncCallback(AsyncReturn), this);
        }
        private void AsyncReturn(IAsyncResult result)
        {
            IsCompleted = true;
            try
            {
                response = _request.EndGetResponse(result);
            }
            catch (WebException e)
            {
                exception = e;
            }
            if (_callback != null)
                _callback(this);
        }
    }
}