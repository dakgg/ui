using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace ViewSystem
{
    /// <summary>
    /// Update Only when visible on canvas
    /// </summary>
    public class UIVisiblity : MonoBehaviour
    {
        bool m_IsVisible = false;

        public static void RegisterUpdate(IViewUpdateListener updateReceiver, GameObject go)
        {
            if (updateReceiver == null) return;
            var updator = go.GetComponent<UIVisiblity>();
            if (updator == null)
            {
                updator = go.AddComponent<UIVisiblity>();
                updator.hideFlags = HideFlags.HideInInspector;
            }
            updator.m_UpdateListeners.Add(updateReceiver);
            updator.RefreshEnable();
        }

        public static void RegisterVisible(IViewVisibleListener visibleReceiver, GameObject go)
        {
            if (visibleReceiver == null) return;
            var updator = go.GetComponent<UIVisiblity>();
            if (updator == null)
            {
                updator = go.AddComponent<UIVisiblity>();
                updator.hideFlags = HideFlags.HideInInspector;
            }
            updator.m_VislbleListeners.Add(visibleReceiver);
            visibleReceiver.OnViewVisible(updator.m_IsVisible);
        }

        List<IViewUpdateListener> m_UpdateListeners = new List<IViewUpdateListener>();
        List<IViewVisibleListener> m_VislbleListeners = new List<IViewVisibleListener>();

        private void Update()
        {
            for (int i = m_UpdateListeners.Count - 1; i >= 0; i--)
            {
                if (m_UpdateListeners[i] == null)
                {
                    m_UpdateListeners.RemoveAt(i);
                    if (m_UpdateListeners.Count == 0)
                    {
                        RefreshEnable();
                        break;
                    }
                    else
                    {
                        continue;
                    }
                }
                m_UpdateListeners[i].OnViewUpdate();
            }
        }

        private void RefreshEnable()
        {
            enabled = m_IsVisible && m_UpdateListeners.Count > 0;
        }

        private void OnCanvasHierarchyChanged()
        {
            var canvas = GetComponentInParent<Canvas>();
            var wasVisible = m_IsVisible;
            m_IsVisible = canvas != null && canvas.enabled;
            if(wasVisible != m_IsVisible)
            {
                for (int i = m_VislbleListeners.Count - 1; i >= 0; i--)
                {
                    if (m_VislbleListeners[i] == null)
                    {
                        m_VislbleListeners.RemoveAt(i);
                        continue;
                    }
                    m_VislbleListeners[i].OnViewVisible(m_IsVisible);
                }
            }
            RefreshEnable();
        }
    }

}