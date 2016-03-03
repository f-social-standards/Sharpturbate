using System;
using System.Net;

namespace Sharpturbate.Core.Extensions
{
    public static class UriExtensions
    {
        public static bool IsAvailable(this Uri url)
        {
            try
            {
                var request = WebRequest.Create(url);
                request.Method = "HEAD";

                using (var response = (HttpWebResponse) request.GetResponse())
                {
                    return response.StatusCode == HttpStatusCode.OK && response.ContentLength > 0;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}