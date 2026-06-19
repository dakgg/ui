using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace ViewSystem
{
    /// <summary>
    /// base class of all view
    /// </summary>
    public class ViewBase : MonoBehaviour
    {
        [System.Serializable]
        public struct ViewPropertyParams
        {
            /// <summary>
            /// functionality to hide behind vies
            /// </summary>
            public bool HideBehindViews;
            /// <summary>
            /// is this canvas global canvas? (Dont destory on scene load);
            /// </summary>
            public bool IsGlobal;
            /// <summary>
            /// this view's layertype
            /// </summary>
            public ViewLayer.Type LayerType;
        }

        public virtual ViewPropertyParams GetViewProperty() => default;
        
        private int m_LockCount = 0;
        private bool m_InputAllowedByManager = false;
        private bool m_InitialFocusCalled = false;
        
        public bool IsViewActive { get; private set; } = false;
        public bool IsViewFocused { get; private set; } = false;
        public bool InputLocked => m_LockCount > 0 || !m_InputAllowedByManager;
        private Canvas m_Canvas;
        private CanvasGroup m_CanvasGroup;
        internal int CanvasOrderInternal { get => m_Canvas.sortingOrder; set => m_Canvas.sortingOrder = value; }
        public ViewRequestBase CurrentRequestBase { get; internal set; }


        //to call add/remove callback
        internal void SetAddOrRemoveInternal(bool add)
        {
            IsViewActive = add;
            if (add)
            {
                if (this is IViewAddedListener listener) listener.OnViewAdded();
            }
            else
            {
                if (this is IViewRemovedListener listener) listener.OnViewRemoved();
            }
        }

        //back button control
        internal void SetBackButtonInternal()
        {
            if (m_CanvasGroup.blocksRaycasts)
            {
                if (this is IViewBackButtonListener listener)
                {
                    listener.OnViewBackButton();
                }
                else
                {
                    CurrentRequestBase.DoneInternal(false);
                }
            }
        }

        //focus control, when this view has become currentview or vise versa
        internal void SetFocusInternal(bool focus)
        {
            IsViewFocused = focus;
            if (focus)
            {
                if (!m_InitialFocusCalled && this is IViewInitialFocusListener initialListener)
                {
                    initialListener.OnViewInitialFocus();
                    m_InitialFocusCalled = true;
                }
                if (this is IViewFocusListener listener) listener.OnViewFocus(true);
            }
            else
            {
                if (this is IViewFocusListener listener) listener.OnViewFocus(false);
            }
        }

        internal void SetInputAllowedInternal(bool allow)
        {
            m_InputAllowedByManager = allow;
            var allowed = m_LockCount <= 0 && m_InputAllowedByManager;
            m_CanvasGroup.blocksRaycasts = allowed;
        }

        //visiblity control, used when active but background
        internal void SetCanvasVisibleInternal(bool visible)
        {
            m_Canvas.enabled = visible;
        }

        //active control, used when added/removed
        internal void SetViewActiveInternal(bool active)
        {
            SetViewActive(active);
        }

        //called by self or view manager when it need to be active
        protected virtual void SetViewActive(bool active)
        {
            gameObject.SetActive(active);
        }

        private void ValidateComponents()
        {
            //init canvas
            m_Canvas = GetComponent<Canvas>();
            if (m_Canvas == null) m_Canvas = gameObject.AddComponent<Canvas>();
            m_Canvas.overrideSorting = true;

            //init raycaster
            var raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster == null) gameObject.AddComponent<GraphicRaycaster>();

            //init canvasgroup
            m_CanvasGroup = GetComponent<CanvasGroup>();
            if (m_CanvasGroup == null) m_CanvasGroup = gameObject.AddComponent<CanvasGroup>();
            m_CanvasGroup.ignoreParentGroups = true;
        }

        public void SetInputEnable(bool enable)
        {
            m_LockCount += enable ? -1 : 1;
            m_CanvasGroup.blocksRaycasts = m_LockCount <= 0 && m_InputAllowedByManager;
        }

        public void EscapeUI(bool escape = true)
        {
            if (m_CanvasGroup) m_CanvasGroup.alpha = escape ? 0 : 1;
        }

        
        [DisallowMultipleComponent]
        [DefaultExecutionOrder(ExecutionOrder.FASTEST)]
        public class ViewEventCaller : MonoBehaviour
        {   
            ViewBase m_TargetView;
            string m_ViewName;
            AsyncOperationHandle m_Handle;
            bool m_IsApplicationQuitting;
            
            public void Setup(string viewName, ViewBase viewBase, AsyncOperationHandle handle)
            {
                m_ViewName = viewName;
                m_TargetView = viewBase;
                m_Handle = handle;
                
                viewBase.ValidateComponents();

                //when loaded by scene. we don't rebuild entire ui. so disable canvas first
                //will be enabled later in Add
                ViewManager.TrackView(viewName, m_TargetView);
                
                m_TargetView.gameObject.AddComponent<ServiceLocator>();

                //register updater listener if needed
                if (m_TargetView is IViewUpdateListener updatelistener) UIVisiblity.RegisterUpdate(updatelistener, gameObject);

                //register visible listener if needed
                if (m_TargetView is IViewVisibleListener visibleListener) UIVisiblity.RegisterVisible(visibleListener, gameObject);
                
                m_TargetView.SetViewActiveInternal(false);
            }

            private void OnApplicationQuit()
            {
                m_IsApplicationQuitting = true;
            }

            private void OnDestroy()
            {
                //skip when is quitting
                if (m_IsApplicationQuitting) return;
                ViewManager.UntrackView(m_ViewName);
                Addressables.Release(m_Handle);
            }
        }
    }

    public class ModalView : ViewBase
    {
        [SerializeField] AnimationSet m_OpenAnimationSet;

        public override ViewPropertyParams GetViewProperty()
        {
            return new ViewPropertyParams()
            {
                IsGlobal = false,
                HideBehindViews = false,
                LayerType = ViewLayer.Type.Default,
            };
        }

        public Coroutine CoPlayModalAnimationSet(bool isOpenAni = true)
        {
            if (m_OpenAnimationSet == null) return null;
            if (isOpenAni) return StartCoroutine(m_OpenAnimationSet.CoPlayAnimation(isOpenAni));
            return StartCoroutine(CoPlayCloseModalAni());
        }

        IEnumerator CoPlayCloseModalAni()
        {
            if (m_OpenAnimationSet == null) yield break;
            var view = ViewManager.CurrentView;
            view.SetInputEnable(false);
            yield return m_OpenAnimationSet.CoPlayAnimation(false);
            view.SetInputEnable(true);
        }
    }

    public class PageView : ViewBase
    {
        [SerializeField] List<AnimationSet> m_OpenAnimationSets;

        public override ViewPropertyParams GetViewProperty()
        {
            return new ViewPropertyParams()
            {
                IsGlobal = false,
                LayerType = ViewLayer.Type.Default,
                HideBehindViews = true,
            };
        }

        public void PlayOpenAnimationSet(bool isOpen = true)
        {
            foreach (var animSet in m_OpenAnimationSets)
            {
                if(animSet.Animation == null)
                {
                    Debug.LogWarning("AnimationSet animation is null");
                    continue;
                } 
                if (!animSet.Animation.GetComponentInParent<UIAnimation>(true).isActiveAndEnabled) continue;
                StartCoroutine(animSet.CoPlayAnimation(isOpen));
            }
        }
    }

    [System.Serializable]
    public class AnimationSet
    {
        [SerializeField] public UIAnimation Animation;
        [SerializeField] public AnimationClip FoldAnimationClip;
        [SerializeField] public AnimationClip UnfoldAnimationClip;

        public IEnumerator CoPlayAnimation(bool isOpen)
        {
            if (Animation == null) throw new System.Exception("Animation is null");
            
            Animation.SampleProgress(0);
            if (isOpen)
            {
                if (UnfoldAnimationClip != null) Animation.AnimationToPlay = UnfoldAnimationClip;
                else throw new System.Exception("FoldAnimationClip is null");
            }
            else
            {
                if (FoldAnimationClip != null) Animation.AnimationToPlay = FoldAnimationClip;
                else throw new System.Exception("UnfoldAnimationClip is null");
            }

            yield return Animation.ShowAnimation();
        }
    }
}