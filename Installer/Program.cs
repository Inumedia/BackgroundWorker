using System;
using TaskScheduler;

namespace Installer
{
    class Program
    {
        static void Main(string[] args)
        {
            TaskScheduler.TaskScheduler mScheduler = new TaskScheduler.TaskScheduler();

            mScheduler.Connect();

            ITaskFolder mFolder = mScheduler.GetFolder("");
            bool CreateNew = !TaskExists(mFolder);
            if (CreateNew)
            {
                IRegisteredTask mTask = mFolder.RegisterTask(null, "BackgroundWorker", 0x6, null, null, _TASK_LOGON_TYPE.TASK_LOGON_INTERACTIVE_TOKEN);
            }
        }

        static bool TaskExists(ITaskFolder mFolder)
        {
            bool mFound = false;
            IRegisteredTaskCollection mTasks = mFolder.GetTasks(0);
            foreach (IRegisteredTask mTask in mTasks)
                mFound |= mTask.Name == "BackgroundWorker";
            return mFound;
        }
    }
}
