using System;
using System.Threading;
using System.Threading.Tasks;

namespace SIPSorcery.Net;

public static class TaskExtensions
{
    public static Task<TResult> ToApm<TResult>(this Task<TResult> task, AsyncCallback callback, object state) 
    { 
        if (task.AsyncState == state) 
        { 
            if (callback != null) 
            { 
                task.ContinueWith(delegate { callback(task); }, 
                    CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default); 
            } 
            return task; 
        }

        var tcs = new TaskCompletionSource<TResult>(state); 
        task.ContinueWith(delegate 
        { 
            if (task.IsFaulted)
            {
                tcs.TrySetException(task.Exception.InnerExceptions);
            }
            else if (task.IsCanceled)
            {
                tcs.TrySetCanceled();
            }
            else
            {
                tcs.TrySetResult(task.Result);
            }

            if (callback != null)
            {
                callback(tcs.Task);
            }
        }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);  
        return tcs.Task; 
    }
}
