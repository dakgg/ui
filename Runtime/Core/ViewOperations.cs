using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ViewSystem
{
    public abstract class ViewRequestBase : CustomYieldInstruction, IAwaiterBase
    {
        public event EventHandler OnDone;
        private bool m_IsDone = false;
        
        public string ViewName { get; private set; }
        public ViewBase View { get; private set; }
        public bool Succeeded { get; private set; }
        public bool DoneRequested { get; private set; }
        
        public override bool keepWaiting => !m_IsDone;
        public bool IsCompleted => m_IsDone;
        public bool IsOpened => View != null;
        
        public ViewRequestBase(string view)
        {
            ViewName = view;
        }

        internal void DoneInternal(bool success)
        {
            if (DoneRequested) return;
            DoneRequested = true;
            Succeeded = success;
            m_IsDone = true;

            ViewManager.DoneImmediately(ViewName);
            
            OnDone?.Invoke(this, null);
            OnDone = null;
        }
        
        public void OnCompleted(Action continuation) => ViewManager.OnContinuation(this, continuation);
        public void UnsafeOnCompleted(Action continuation) => ViewManager.OnContinuation(this, continuation);
        internal void SetViewInternal(ViewBase view) => View = view;
    }
    public class ViewRequest : ViewRequestBase, IAwaiter<ViewRequest>
    {
        ViewRequest IAwaiter<ViewRequest>.GetResult() => this;
        public IAwaiter<ViewRequest> GetAwaiter() => this;

        public ViewRequest(string viewName) : base(viewName) { }
        
        public static ViewRequest Open(string viewName, Action<ViewBase> openAction = null, bool instant = false)
        {
            var request = new ViewRequest(viewName);
            ViewManager.Instance.OpenInternal(request, openAction, instant);
            return request;
        }
        
        public static ViewRequest Open<TView>(Action<TView> openAction = null, bool instant = false) where TView : ViewBase
        {
            var request = new ViewRequest(ViewNames.GetName(typeof(TView)));
            ViewManager.Instance.OpenInternal(request, vb => openAction((TView)vb), instant);
            return request;
        }

        public static ViewRequestBase OpenOrInvoke<TView>(Action<TView> openAction, ViewBase viewBase) where TView : ViewBase
        {
            if(viewBase is TView view)
            {
                openAction?.Invoke(view);
                return view.CurrentRequestBase;
            }

            ViewManager.ActiveComplete<TView>();
            return Open(openAction);
        }
    }

    public class ViewRequest<T> : ViewRequestBase, IAwaiter<ViewRequest<T>>
    {
        ViewRequest<T> IAwaiter<ViewRequest<T>>.GetResult() => this;
        public IAwaiter<ViewRequest<T>> GetAwaiter() => this;

        public T Result { get; private set; }
        public void SetResult(T result) => Result = result;
        public ViewRequest(string viewName) : base(viewName) { }
        
        public static ViewRequest<T> Open(string viewName, Action<ViewBase> openAction = null, bool instant = false)
        {
            var request = new ViewRequest<T>(viewName);
            ViewManager.Instance.OpenInternal(request, openAction, instant);
            return request;
        }
        
        public static ViewRequest<T> Open<TView>(Action<TView> openAction = null, bool instant = false) where TView : ViewBase, IViewResult<T>
        {
            var request = new ViewRequest<T>(ViewNames.GetName(typeof(TView)));
            ViewManager.Instance.OpenInternal(request, openAction == null ? null : vb => openAction((TView)vb), instant);
            return request;
        }
        
        public bool TryGetResult(out T result)
        {
            if(IsCompleted && Succeeded) 
            {
                result = Result;
                return true;
            } 
            else
            {
                result = default;
                return false;
            }
        }
    }

    public static class ViewResultExtensions
    {
        public static void Complete<T>(this T view) where T : ViewBase
        {
            if (view.CurrentRequestBase == null) throw new Exception("Currently not pushed");
            view.CurrentRequestBase.DoneInternal(false);
        }

        public static void Success<T>(this T view) where T : ViewBase, IViewResult
        {
            if (view.CurrentRequestBase == null) throw new Exception("Currently not pushed");
            
            if (view.CurrentRequestBase is ViewRequest typedRequest)
            {
                typedRequest.DoneInternal(true);
            }
            else
            {
                var genericParam = view.CurrentRequestBase.GetType().GetGenericArguments()[0];
                throw new Exception($"Invlaid Result Type Expected {genericParam} but received Void");
            }
        }

        public static void Success<TView, TResult>(this TView view, TResult result) where TView : ViewBase, IViewResult<TResult>
        {
            if (view.CurrentRequestBase == null) throw new System.Exception("Currently not pushed");
            
            if (view.CurrentRequestBase is ViewRequest<TResult> typedRequest)
            {
                typedRequest.SetResult(result);
                typedRequest.DoneInternal(true);
            }
            else if (view.CurrentRequestBase is ViewRequest typedRequestWithoutResult) //allow empty result
            {
                typedRequestWithoutResult.DoneInternal(true);
            }
            else
            {
                var genericParam = view.CurrentRequestBase.GetType().GetGenericArguments()[0];
                throw new Exception($"Invalid Result Type Expected {genericParam} but received {typeof(TResult)}");
            }
        }
    }

    /// <summary>
    /// attributes that indicates loading strategy
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ViewLoadAttribute : Attribute
    {
        public string LoadPath { get; private set; }

        public ViewLoadAttribute(string loadPath)
        {
            LoadPath = loadPath;
        }
    }

    public interface IAwaiter<out TResult> : IAwaiterBase
    {
        TResult GetResult();
        IAwaiter<TResult> GetAwaiter();
    }

    public interface IAwaiter : IAwaiterBase
    {
        void GetResult();
        IAwaiter GetAwaiter();
    }

    public interface IAwaiterBase : ICriticalNotifyCompletion
    {
        event EventHandler OnDone;
        bool IsCompleted { get; }
    }

    public interface IViewResult<T> { }
    public interface IViewResult { }
}
