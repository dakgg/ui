using UnityEngine;
using System.Threading.Tasks;
using System.Collections;

public static class TaskExtension
{
    public static async void Forget(this Awaitable awaitable)
    {
        try
        {
            await awaitable;
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
    }
    
    public static async void Forget(this Task task)
    {
        try
        {
            await task;
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
    }

    public static async void Forget(this ValueTask task)
    {
        try
        {
            await task;
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
        }
    }
    
    public static bool IsCompletedSuccessfully(this Task task)
    {
        return task.IsCompleted && !(task.IsCanceled || task.IsFaulted);
    }

    public static bool TryGetResult<T>(this Task<T> task, out T result)
    {
        if(task.IsCompletedSuccessfully())
        {
            result = task.Result;
            return true;
        }
        else
        {
            result = default;
            return false;
        }
    }

    private class TaskHelper : MonoBehaviour {}
    private static TaskHelper s_Helper;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InitializeInRuntime()
    {
        s_Helper = new GameObject("_TaskHelper").AddComponent<TaskHelper>();
        Object.DontDestroyOnLoad(s_Helper.gameObject);
    }

    public static Task ToTask(this Coroutine coroutine)
    {
        var tcs = new TaskCompletionSource<bool>();
        s_Helper.StartCoroutine(_CoWaitForDone());
        return tcs.Task;

        IEnumerator _CoWaitForDone()
        {
            yield return coroutine;
            tcs.SetResult(true);
        }
    }

    public static Coroutine ToCoroutine(this Task task)
    {
        return s_Helper.StartCoroutine(_CoWaitForDone());

        IEnumerator _CoWaitForDone()
        {
            while(!task.IsCompleted) yield return null;
            if(task.IsCanceled) Debug.LogException(new System.OperationCanceledException("Task has been cancelled"));
            if(task.IsFaulted) Debug.LogException(task.Exception);
        }
    }

}