using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ViewSystem
{
    public class ViewManager : MonoBehaviour, IViewLayerListener, IServiceLocatorComponent, IServiceLocatorSetupComponent
    {
        public static int UnityThreadId { get; private set; }
        const int MAX_COUNT_PER_LAYER = 100;

        internal static ViewManager Instance { get; private set; }
        private readonly List<ViewLayer> m_Layers = new();
        private readonly Dictionary<ViewLayer.Type, ViewLayer> m_LayerDict = new();

        public static UnityEvent OnOrientationChange = new UnityEvent();
        public static ViewBase CurrentView { get => Instance == null ? null : Instance.m_Focused; }
        public static ViewBase CurrentViewPage { get => Instance == null ? null : Instance.m_CurrentViewPage; }

        [SerializeField] ViewBase m_Focused;
        [SerializeField] ViewBase m_CurrentViewPage;

        //currently Instantiated and not destroyed view
        Dictionary<string, ViewBase> m_AvailableViews = new();
        Dictionary<string, (ViewRequestBase request, ViewLayer layer)> m_ActiveViewToLayer = new();
        Dictionary<string, OverlayView> m_AvailableOverlayViews = new();
        Dictionary<string, AsyncOperationHandle<ViewBase>> m_LoadingViews = new();

        List<IViewOperationListener> m_ViewOperationListeners = new List<IViewOperationListener>();

        ScreenOrientation m_CurrentOrientation;
        bool m_OrientationChagnedLastFrame = false;
        int m_CurrentStaticViewSortLayer = 0;

        public bool EscapeControl = false;

        private void Awake()
        {
            Instance = this;
            m_CurrentOrientation = Screen.orientation;
            AddLayer(ViewLayer.Type.Default);
            AddLayer(ViewLayer.Type.Special);
            UnityThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            DontDestroyOnLoad(gameObject);
            m_CurrentStaticViewSortLayer = m_Layers.Count * MAX_COUNT_PER_LAYER;
        }

        public void AddOperationListener(IViewOperationListener listener)
        {
            m_ViewOperationListeners.Add(listener);
        }

        public void RemoveOperationListener(IViewOperationListener listener)
        {
            m_ViewOperationListeners.Remove(listener);
        }


        private ViewLayer AddLayer(ViewLayer.Type type)
        {
            var minOrder = m_Layers.Count * MAX_COUNT_PER_LAYER;
            var maxOrder = minOrder + MAX_COUNT_PER_LAYER;
            var layer = new ViewLayer(this, minOrder, maxOrder);
            m_Layers.Add(layer);
            m_LayerDict.Add(type, layer);
            return layer;
        }

        private bool TryGetTopView(out ViewBase foundView)
        {
            for (int i = m_Layers.Count - 1; i >= 0; i--)
            {
                if (!m_Layers[i].TryGetTopView(out foundView)) continue;
                return true;
            }
            foundView = default;
            return false;
        }

        public void CloseAll()
        {
            for (int i = m_Layers.Count - 1; i >= 0; i--)
            {
                m_Layers[i].CloseAll();
            }
        }

        public static T Get<T>() where T : ViewBase
        {
            if (!Instance.m_AvailableViews.TryGetValue(ViewNames.GetName(typeof(T)), out var view))
            {
                throw new Exception("View not loaded, try Ensure(Aysnc) First");
            }
            return view as T;
        }

        public static bool TryGet<T>(out T view) where T : ViewBase
        {
            var result = Instance.m_AvailableViews.TryGetValue(ViewNames.GetName(typeof(T)), out var found);
            view = found as T;
            return result;
        }

        public static bool TryGetActive<T>(out T view) where T : ViewBase
        {
            var result = Instance.m_AvailableViews.TryGetValue(ViewNames.GetName(typeof(T)), out var found) && found.IsViewActive;
            view = found as T;
            return result;
        }

        public static void ActiveComplete<T>() where T : ViewBase
        {
            if (Instance.m_AvailableViews.TryGetValue(ViewNames.GetName(typeof(T)), out var found) && found.IsViewActive)
            {
                found.Complete();
            }
        }

        public static T GetOverlay<T>() where T : OverlayView
        {
            if (!Instance.m_AvailableOverlayViews.TryGetValue(ViewNames.GetName(typeof(T)), out var view))
            {
                throw new Exception("view not loaded, try Ensure(Aysnc) First");
            }
            return view as T;
        }

        public static bool TryGetOverlay<T>(out T view) where T : OverlayView
        {
            var result = Instance.m_AvailableOverlayViews.TryGetValue(ViewNames.GetName(typeof(T)), out var found);
            view = found as T;
            return result;
        }

        public static void AddOrReplaceOverlay<T>(T view) where T : OverlayView
        {
            var canvas = view.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            var viewName = ViewNames.GetName(typeof(T));

            if (!Instance.m_AvailableOverlayViews.TryGetValue(viewName, out var prev))
            {
                canvas.sortingOrder = Instance.m_CurrentStaticViewSortLayer;
                Instance.m_CurrentStaticViewSortLayer++;
                Instance.m_AvailableOverlayViews.Add(viewName, view);
            }
            else
            {
                canvas.sortingOrder = prev.GetComponent<Canvas>().sortingOrder;
                Instance.m_AvailableOverlayViews[viewName] = view;
                Destroy(prev);
            }
        }

        public Coroutine Clear()
        {
            //clear loading views
            m_LoadingViews.Values.ToList().ForEach(handle => handle.ReleaseIfValid());
            m_LoadingViews.Clear();

            var notActiveViews = m_AvailableViews.Values.Where(view => !view.IsViewActive).ToArray();
            foreach (var view in notActiveViews) Destroy(view.gameObject);
            return StartCoroutine(_WaitForViewDestroyed());

            IEnumerator _WaitForViewDestroyed() { yield return null; }
        }

        internal static void TrackView(string viewName, ViewBase view)
        {
            if (view == null) throw new NullReferenceException();

            if (!Instance.m_AvailableViews.TryAdd(viewName, view))
            {
                throw new Exception("Same type of view already registered");
            }
        }

        internal static void UntrackView(string viewName)
        {
            DoneImmediately(viewName);
            if (!Instance.m_AvailableViews.Remove(viewName, out _))
            {
                throw new Exception("view is not tacked or tracked vie is not the same one");
            }
        }

        internal static void DoneImmediately(string viewName)
        {
            if (Instance.m_ActiveViewToLayer.Remove(viewName, out var tuple))
            {
                if (!tuple.request.IsCompleted)
                {
                    tuple.request.DoneInternal(false);
                }
                tuple.layer.PopInternal(tuple.request);
            }
        }

        public static void Preload(string viewName)
        {
            Instance.PreloadInternal(viewName);
        }

        private void PreloadInternal(string viewName)
        {
            //already loaded
            if (m_AvailableViews.ContainsKey(viewName)) return;

            //in any case, this is leading one
            if (!m_LoadingViews.Remove(viewName, out var handle))
            {
                handle = LoadViewAddressableInternal(viewName);
            }

            handle.WaitForCompletion();

            //this is leading one so instantiate
            InstantiateViewInternal(viewName, handle);
        }

        internal void OpenInternal(ViewRequestBase req, Action<ViewBase> openAction, bool instant)
        {
            _OpenInternalAsync().Forget();
            async Awaitable _OpenInternalAsync()
            {
                if (!m_AvailableViews.TryGetValue(req.ViewName, out var view))
                {
                    if (!m_LoadingViews.TryGetValue(req.ViewName, out var handle))
                    {
                        handle = LoadViewAddressableInternal(req.ViewName);
                        m_LoadingViews.Add(req.ViewName, handle);
                    }

                    if (instant)
                    {
                        handle.WaitForCompletion();
                    }
                    else
                    {
                        var currentView = CurrentView;
                        currentView?.SetInputEnable(false);
                        await handle.Task;
                        currentView?.SetInputEnable(true);
                    }

                    //if remove success, it's leading one
                    if (m_LoadingViews.Remove(req.ViewName))
                    {
                        if (handle.Status != AsyncOperationStatus.Succeeded)
                        {
                            req.DoneInternal(false);
                            Debug.LogError($"View Operation Has Failed ! Name : {req.ViewName}");
                            return;
                        }

                        view = InstantiateViewInternal(req.ViewName, handle);
                    }
                }

                var layer = m_LayerDict[view.GetViewProperty().LayerType];
                if (m_ActiveViewToLayer.TryGetValue(req.ViewName, out _))
                {
                    Debug.LogError($"View Operation Has Failed ! Name : {req.ViewName}");
                    return;
                }

                m_ActiveViewToLayer.Add(req.ViewName, (req, layer));
                req.SetViewInternal(view);

                req.View.CurrentRequestBase = req;
                layer.PushInternal(req);

                if (openAction == null)
                {
                    if (view is IViewOpenWithoutActionListener viewOpenListener)
                    {
                        viewOpenListener.OnViewOpenWithoutAction();
                    }
                }
                else
                {
                    openAction?.Invoke(req.View);
                }
            }
        }

        private AsyncOperationHandle<ViewBase> LoadViewAddressableInternal(string viewName)
        {
            var req = BundleUtility.LoadAsync<GameObject>(viewName);
            return Addressables.ResourceManager.CreateChainOperation(req, _OnComplete);
            static AsyncOperationHandle<ViewBase> _OnComplete(AsyncOperationHandle<GameObject> handle)
            {
                if (handle.Status != AsyncOperationStatus.Succeeded)
                {
                    return Addressables.ResourceManager.CreateCompletedOperation<ViewBase>(null, "Loading view failed");
                }

                var viewBase = handle.Result.GetComponent<ViewBase>();
                if (viewBase == null)
                {
                    handle.ReleaseIfValid();
                    return Addressables.ResourceManager.CreateCompletedOperation<ViewBase>(null, "Loading view failed");
                }

                return Addressables.ResourceManager.CreateCompletedOperation(viewBase, string.Empty);
            }
        }

        private ViewBase InstantiateViewInternal(string viewName, AsyncOperationHandle<ViewBase> handle)
        {
            var viewBase = handle.Result;
            var parent = viewBase.GetViewProperty().IsGlobal
                ? ViewRootCanvas.GetGlobalRoot()
                : ViewRootCanvas.GetSceneRoot();

            var inst = Instantiate(viewBase, parent, false);
            inst.gameObject.AddComponent<ViewBase.ViewEventCaller>().Setup(viewName, inst, handle);
            return inst;
        }

        private void Update()
        {
            if (!EscapeControl && m_Focused != null && Input.GetKeyDown(KeyCode.Escape))
                m_Focused.SetBackButtonInternal();

            //orientation callback does not immedietly applied to safearea
            //we need to wait a frame
            if (m_CurrentOrientation != Screen.orientation)
            {
                m_CurrentOrientation = Screen.orientation;
                m_OrientationChagnedLastFrame = true;
            }
            else if (m_OrientationChagnedLastFrame)
            {
                OnOrientationChange.Invoke();
                m_OrientationChagnedLastFrame = false;
            }
        }

        internal static void OnContinuation(IAwaiterBase awaitor, System.Action continuation)
        {
            if (System.Threading.Thread.CurrentThread.ManagedThreadId != UnityThreadId)
            {
                throw new System.Exception("Should be awaited in UnityMainThread");
            }

            if (awaitor.IsCompleted) continuation.Invoke();
            else awaitor.OnDone += (_, _) => continuation.Invoke();
        }

        public static void Refresh()
        {
            Instance.RefreshInternal();
        }

        private ViewBase RefreshInternal()
        {
            var behindVisible = true;
            var newFocused = default(ViewBase);
            for (int i = m_Layers.Count - 1; i >= 0; i--)
            {
                m_Layers[i].Refresh(ref behindVisible, ref newFocused);
            }
            return newFocused;
        }

        public void OnViewAdded(ViewBase view)
        {
            var newFocused = RefreshInternal();
            if (newFocused != m_Focused)
            {
                if (m_Focused != null) m_Focused.SetFocusInternal(false);
                m_Focused = newFocused;
                if (m_Focused is PageView page) m_CurrentViewPage = page;
                m_Focused.SetFocusInternal(true);
            }
            m_ViewOperationListeners.ForEach(listener => listener.OnViewAdded(view));
        }

        public void OnViewRemoved(ViewBase view)
        {
            var newFocused = RefreshInternal();
            if (newFocused != m_Focused)
            {
                if (m_Focused != null) m_Focused.SetFocusInternal(false);
                m_Focused = newFocused;
                if (m_Focused is PageView page) m_CurrentViewPage = page;
                if (m_Focused != null) m_Focused.SetFocusInternal(true);
            }

            m_ViewOperationListeners.ForEach(listener => listener.OnViewRemoved(view));
        }
    }

    public interface IViewOperationListener
    {
        void OnViewAdded<T>(T viewBase) where T : ViewBase;
        void OnViewRemoved<T>(T viewBase) where T : ViewBase;
    }
}