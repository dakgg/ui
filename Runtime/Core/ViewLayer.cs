using System;
using System.Collections.Generic;

namespace ViewSystem
{
    public class ViewLayer
    {
        public enum Type { Default, Special }
        public readonly int MinSortOrder;
        public readonly int MaxSortOrder;

        readonly List<ViewRequestBase> m_ActiveViews = new();
        readonly List<IViewLayerListener> m_Listeners = new();

        readonly int m_ViewGap = 10;
        public ViewLayer(IViewLayerListener listener, int minOrder, int maxOrder)
        {
            AddListener(listener);
            MinSortOrder = minOrder;
            MaxSortOrder = maxOrder;
        }

        public void AddListener(IViewLayerListener listener)
        {
            if (!m_Listeners.Contains(listener)) m_Listeners.Add(listener);
        }

        public bool TryGetTopView(out ViewBase view)
        {
            if (m_ActiveViews.Count == 0)
            {
                view = default;
                return false;
            }
            else
            {
                view = m_ActiveViews[^1].View;
                return true;
            }
        }

        internal void PushInternal(ViewRequestBase viewRequest)
        {
            if (viewRequest == null) throw new NullReferenceException();

            if (TryGetTopView(out var topView) && topView.CanvasOrderInternal >= MaxSortOrder) throw new Exception("Max sorting layer exceeded");

            if (m_ActiveViews.Contains(viewRequest)) throw new Exception("views is already pushed");

            //set order
            viewRequest.View.CanvasOrderInternal = topView == null ? MinSortOrder : topView.CanvasOrderInternal + m_ViewGap;
            m_ActiveViews.Add(viewRequest);
            viewRequest.View.SetAddOrRemoveInternal(true);
            viewRequest.View.SetViewActiveInternal(true);
            m_Listeners.ForEach(listener => listener.OnViewAdded(viewRequest.View));
        }

        internal void PopInternal(ViewRequestBase viewRequest)
        {
            if (viewRequest == null) throw new NullReferenceException();
            
            var removeIndex = m_ActiveViews.IndexOf(viewRequest);
            if (removeIndex < 0) throw new Exception("view not found");

            //remove first
            m_ActiveViews.RemoveAt(removeIndex);

            //re-align order
            for (var i = removeIndex; i < m_ActiveViews.Count; i++)
            {
                m_ActiveViews[i].View.CanvasOrderInternal -= m_ViewGap;
            }
            
            m_Listeners.ForEach(listener => listener.OnViewRemoved(viewRequest.View));
            viewRequest.View.SetAddOrRemoveInternal(false);
            viewRequest.View.SetViewActiveInternal(false);
        }

        public void Refresh(ref bool childVisible, ref ViewBase focusedView)
        {
            for (int i = m_ActiveViews.Count - 1; i >= 0; i--)
            {
                var current = m_ActiveViews[i].View;
                current.SetCanvasVisibleInternal(childVisible);
                if (childVisible) childVisible = !current.GetViewProperty().HideBehindViews;
                if (focusedView == null) focusedView = current;
                current.SetInputAllowedInternal(focusedView == current);
            }
        }


        public void CloseAll()
        {
            while (m_ActiveViews.Count > 0)
            {
                if(m_ActiveViews[^1].View is IViewRoot) return;
                m_ActiveViews[^1].View.Complete();
            }
        }
    }

    public interface IViewLayerListener
    {
        void OnViewAdded(ViewBase view);
        void OnViewRemoved(ViewBase view);
    }
}
