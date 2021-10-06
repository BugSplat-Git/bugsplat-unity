using System.Collections;
using System.Threading.Tasks;

namespace BugSplatUnity.RuntimeTests
{
    public static class TaskExtensions
    {
        public static IEnumerator AsCoroutine(this Task task)
        {
            while (!task.IsCompleted) yield return null;

            // Will throw if task faults
            task.GetAwaiter().GetResult();
        }
    }
}
