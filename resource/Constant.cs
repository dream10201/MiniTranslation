using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniTranslation.resource
{
    class Constant
{
        //https://translate.google.cn/translate_a/single?ie=UTF-8&oe=UTF-8&client=gtx&otf=1&ssel=0&tsel=0&kc=1&dt=t&hl=en&sl=
        public const String TranslateURL = "https://translate.google.cn/translate_a/single?ie=UTF-8&oe=UTF-8&tk=&client=gtx&dt=t&ssel=3&tsel=0&kc=1&otf=1&hl=zh-CN&sl=";
        public const int WM_HOTKEY = 0x312; //窗口消息-热键  
        public const int WM_CREATE = 0x1; //窗口消息-创建  
        public const int WM_DESTROY = 0x2; //窗口消息-销毁
        public const int WM_QUERYENDSESSION = 0x0011;
        public const int HOTKeyID = 0x3572; //热键ID
    }
}
