using System;
using System.IO;
using System.Net;
using System.Text;

namespace MiniTranslation.util
{
    class HttpUtils
    {
        public static string Get(string url)
        {
            StreamReader myreader = null;
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                myreader = new StreamReader(request.GetResponse().GetResponseStream(), Encoding.UTF8);
                string responseText = myreader.ReadToEnd();
                return responseText;
            }
            catch (Exception)
            {
            }
            finally {
                if (myreader != null)
                {
                    //myreader.Dispose();
                    myreader.Close();
                }
            }
            return "";
        }
    }
}
