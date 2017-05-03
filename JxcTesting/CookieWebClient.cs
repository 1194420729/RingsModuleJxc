using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace JxcTesting
{
    class CookieWebClient:WebClient
    {
        private string baseurl = "http://localhost:59422";

        public CookieContainer CookieContainer { get; private set; }

        /// <summary>
        /// This will instanciate an internal CookieContainer.
        /// </summary>
        public CookieWebClient()
        {
            this.CookieContainer = new CookieContainer();

            this.Encoding = Encoding.UTF8;
            this.Headers.Add(HttpRequestHeader.Accept, "json");
            this.Headers.Add(HttpRequestHeader.ContentType, "application/json");
            this.UploadString(baseurl+"/account/UnitTestLogin", "POST", "");

        }
         

        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = base.GetWebRequest(address) as HttpWebRequest;
            if (request == null) return base.GetWebRequest(address);
            request.CookieContainer = CookieContainer;
            return request;
        }
    }
}
