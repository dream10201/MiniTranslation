namespace MiniTranslation.Core
{
    /// <summary>开机自启动，基于任务计划程序的登录触发任务。</summary>
    public static class AutoStart
    {
        private const string TaskName = "MiniTranslation";

        public static bool IsEnabled()
        {
            try
            {
                dynamic service = CreateService();
                return service.GetFolder("\\").GetTask(TaskName) != null;
            }
            catch
            {
                return false;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            try
            {
                dynamic service = CreateService();
                dynamic folder = service.GetFolder("\\");
                if (enabled)
                {
                    dynamic task = service.NewTask(0);
                    // 不限运行时长，且不受电池供电限制，否则笔记本离电时不会启动
                    task.Settings.ExecutionTimeLimit = "PT0S";
                    task.Settings.DisallowStartIfOnBatteries = false;
                    task.Settings.StopIfGoingOnBatteries = false;
                    dynamic trigger = task.Triggers.Create(9); // TASK_TRIGGER_LOGON
                    trigger.UserId = $"{Environment.UserDomainName}\\{Environment.UserName}";
                    dynamic action = task.Actions.Create(0); // TASK_ACTION_EXEC
                    action.Path = Application.ExecutablePath;
                    folder.RegisterTaskDefinition(TaskName, task,
                        6,    // TASK_CREATE_OR_UPDATE
                        null, null,
                        3,    // TASK_LOGON_INTERACTIVE_TOKEN
                        null);
                }
                else
                {
                    folder.DeleteTask(TaskName, 0);
                }
            }
            catch
            {
                // 任务不存在或计划程序服务不可用时忽略
            }
        }

        private static dynamic CreateService()
        {
            dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!)!;
            service.Connect();
            return service;
        }
    }
}
