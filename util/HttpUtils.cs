using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MiniTranslation.util
{
    class HttpUtils
    {
        public static string Get(string url) {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                StreamReader myreader = new StreamReader(request.GetResponse().GetResponseStream(), Encoding.UTF8);
                string responseText = myreader.ReadToEnd();
                myreader.Dispose();
                myreader.Close();
                return responseText;
            }
            catch (Exception)
            {

            }
            return "";
        }
    }
}
