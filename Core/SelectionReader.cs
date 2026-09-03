using System.Runtime.InteropServices;

namespace MiniTranslation.Core
{
    /// <summary>通过 UI Automation 读取前台焦点元素的选中文本，不动剪贴板、不注入按键。</summary>
    public static class SelectionReader
    {
        private const int TextPatternId = 10014;

        /// <summary>UIA 调用可能被目标程序拖住数秒，在后台线程执行并限时，超时或失败返回空串。</summary>
        public static async Task<string> TryGetSelectedTextAsync(int timeoutMs)
        {
            var work = Task.Run(Read);
            var done = await Task.WhenAny(work, Task.Delay(timeoutMs));
            return done == work ? work.Result : "";
        }

        private static string Read()
        {
            try
            {
                var automation = (IUIAutomation)new CUIAutomation();
                automation.GetFocusedElement(out var element);
                if (element == null) return "";
                element.GetCurrentPattern(TextPatternId, out var patternObj);
                if (patternObj is not IUIAutomationTextPattern pattern) return "";
                pattern.GetSelection(out var ranges);
                if (ranges == null) return "";
                ranges.get_Length(out int count);
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < count; i++)
                {
                    ranges.GetElement(i, out var range);
                    range.GetText(-1, out string text);
                    sb.Append(text);
                }
                return sb.ToString().Trim();
            }
            catch
            {
                return ""; // 目标程序不支持 UIA 或调用失败，交给剪贴板方式兜底
            }
        }

        [ComImport, Guid("ff48dba4-60ef-4201-aa87-54103eef594e")]
        private class CUIAutomation
        {
        }

        // 以下接口只声明到用到的方法为止，前面的槽位按 UIAutomationClient.h 顺序占位

        [ComImport, Guid("30cbe57d-d9d0-452a-ab13-7ac5ac4825ee"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IUIAutomation
        {
            void CompareElements();
            void CompareRuntimeIds();
            void GetRootElement();
            void ElementFromHandle();
            void ElementFromPoint();
            void GetFocusedElement(out IUIAutomationElement element);
        }

        [ComImport, Guid("d22108aa-8ac5-49a5-837b-37bbb3d7591e"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IUIAutomationElement
        {
            void SetFocus();
            void GetRuntimeId();
            void FindFirst();
            void FindAll();
            void FindFirstBuildCache();
            void FindAllBuildCache();
            void BuildUpdatedCache();
            void GetCurrentPropertyValue();
            void GetCurrentPropertyValueEx();
            void GetCachedPropertyValue();
            void GetCachedPropertyValueEx();
            void GetCurrentPatternAs();
            void GetCachedPatternAs();
            void GetCurrentPattern(int patternId, [MarshalAs(UnmanagedType.IUnknown)] out object pattern);
        }

        [ComImport, Guid("32eba289-3583-42c9-9c59-3b6d9a1e9b6a"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IUIAutomationTextPattern
        {
            void RangeFromPoint();
            void RangeFromChild();
            void GetSelection(out IUIAutomationTextRangeArray ranges);
        }

        [ComImport, Guid("ce4ae76a-e717-4c98-81ea-47371d028eb6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IUIAutomationTextRangeArray
        {
            void get_Length(out int length);
            void GetElement(int index, out IUIAutomationTextRange range);
        }

        [ComImport, Guid("a543cc6a-f4ae-494b-8239-c814481187a8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IUIAutomationTextRange
        {
            void Clone();
            void Compare();
            void CompareEndpoints();
            void ExpandToEnclosingUnit();
            void FindAttribute();
            void FindText();
            void GetAttributeValue();
            void GetBoundingRectangles();
            void GetEnclosingElement();
            void GetText(int maxLength, [MarshalAs(UnmanagedType.BStr)] out string text);
        }
    }
}
